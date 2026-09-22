// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

public sealed record TaxBand(int RatePpm, long TaxableCents, long TaxCents);

public sealed record InvoiceTotals(
    long SubtotalCents, long TaxableCents, long TaxCents, long TotalCents, bool IsTaxInvoice, IReadOnlyList<TaxBand> Bands);

public static class InvoiceCalculator
{
    // tax is worked out once per rate, not per line, so rounding can't pile up
    public static InvoiceTotals Calculate(IEnumerable<InvoiceLine> lines, bool taxEnabled, int ratePpm, int reducedRatePpm = 0)
    {
        long subtotal = 0, standard = 0, reduced = 0;
        bool anyStandard = false, anyReduced = false;
        foreach (var line in lines)
        {
            var amount = MoneyMath.LineAmount(line.Quantity, line.UnitPriceCents);
            subtotal += amount;
            switch (line.TaxCode)
            {
                case TaxCode.Standard:
                    standard += amount;
                    anyStandard = true;
                    break;
                case TaxCode.Reduced:
                    reduced += amount;
                    anyReduced = true;
                    break;
            }
        }

        var isTax = taxEnabled && (anyStandard || anyReduced);
        var bands = new List<TaxBand>();
        if (isTax && anyStandard) bands.Add(new TaxBand(ratePpm, standard, MoneyMath.TaxOn(standard, ratePpm)));
        if (isTax && anyReduced) bands.Add(new TaxBand(reducedRatePpm, reduced, MoneyMath.TaxOn(reduced, reducedRatePpm)));
        var tax = bands.Sum(b => b.TaxCents);
        return new InvoiceTotals(subtotal, standard + reduced, tax, subtotal + tax, isTax, bands);
    }

    public static InvoiceTotals Totals(this Invoice invoice) =>
        Calculate(invoice.Lines, invoice.TaxEnabled, invoice.TaxRatePpm, invoice.ReducedRatePpm);

    public static string Heading(this Invoice invoice, CountryRules country) =>
        invoice.Kind == InvoiceKind.Quote ? "QUOTE" : invoice.Totals().IsTaxInvoice ? country.TaxedHeading : "INVOICE";
}
