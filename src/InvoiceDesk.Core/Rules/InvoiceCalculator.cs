// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

public sealed record InvoiceTotals(long SubtotalCents, long TaxableCents, long GstCents, long TotalCents, bool IsTaxInvoice)
{
    public string Heading => IsTaxInvoice ? "TAX INVOICE" : "INVOICE";
}

public static class InvoiceCalculator
{
    public static InvoiceTotals Calculate(IEnumerable<InvoiceLine> lines, bool gstEnabled, int rateBasisPoints)
    {
        long subtotal = 0, taxable = 0;
        var anyTaxable = false;
        foreach (var line in lines)
        {
            var amount = MoneyMath.LineAmount(line.Quantity, line.UnitPriceCents);
            subtotal += amount;
            if (line.GstFree) continue;
            taxable += amount;
            anyTaxable = true;
        }

        var isTax = gstEnabled && anyTaxable;
        var gst = isTax ? MoneyMath.GstOn(taxable, rateBasisPoints) : 0;
        return new InvoiceTotals(subtotal, taxable, gst, subtotal + gst, isTax);
    }

    public static InvoiceTotals Totals(this Invoice invoice) =>
        Calculate(invoice.Lines, invoice.GstEnabled, invoice.GstRateBasisPoints);

    public static string Heading(this Invoice invoice) =>
        invoice.Kind == InvoiceKind.Quote ? "QUOTE" : invoice.Totals().Heading;
}
