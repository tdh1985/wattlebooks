// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Services;

public enum ReturnLine { Normal, Total, Result }

public sealed record ReturnBox(string Code, string Label, long Cents, ReturnLine Line = ReturnLine.Normal, bool WholeUnits = false, string? Note = null);

public sealed record TaxReturn(
    ReturnKind Kind, string Title, DateRange Period, IReadOnlyList<ReturnBox> Boxes,
    long NetCents, string NetLabel, IReadOnlyList<CategoryTotal> SalesByCategory)
{
    public long Box(string code) => Boxes.First(b => b.Code == code).Cents;
}

// same cash-basis transactions the dashboard counts, laid out per form
public static class TaxReturns
{
    const string NorthernIreland = "Northern Ireland only";
    const string NotTracked = "Not tracked by InvoiceDesk";

    public static TaxReturn Build(CountryRules country, DateRange period, IReadOnlyList<Transaction> txs, IReadOnlyList<CategoryTotal> salesByCategory)
    {
        var sales = txs.Where(t => t.Direction == Direction.In && period.Contains(t.Date)).ToList();
        var costs = txs.Where(t => t.Direction == Direction.Out && period.Contains(t.Date)).ToList();
        var (boxes, net, label) = country.Return switch
        {
            ReturnKind.NzGst => NewZealand(country, sales, costs),
            ReturnKind.UkVat => UnitedKingdom(sales, costs),
            ReturnKind.CaGstHst => Canada(sales, costs),
            ReturnKind.UsSalesTax => UnitedStates(sales),
            _ => Australia(sales, costs),
        };
        return new TaxReturn(country.Return, country.ReturnTitle, period, boxes, net, label, salesByCategory);
    }

    static (List<ReturnBox>, long, string) Australia(List<Transaction> sales, List<Transaction> costs)
    {
        var g1 = sales.Sum(t => t.AmountCents);
        var a1 = sales.Sum(t => t.TaxCents);
        var b1 = costs.Sum(t => t.TaxCents);
        var net = a1 - b1;
        return ([
            new("G1", "Total sales, including GST", g1),
            new("1A", "GST on sales", a1),
            new("1B", "GST on purchases", b1),
            new("", net >= 0 ? "GST to pay (1A less 1B)" : "GST refund (1B less 1A)", net, ReturnLine.Result),
        ], net, net >= 0 ? "GST to pay" : "GST refund");
    }

    // what a transaction carried no gst on, per row so rounding can't borrow from a real one
    static long ZeroRated(Transaction t, int ratePpm) =>
        Math.Max(0, t.AmountCents - MoneyMath.InclusiveFromTax(t.TaxCents, ratePpm));

    // gst charged is the authority so a part zero-rated sale can't inflate box 8
    static (List<ReturnBox>, long, string) NewZealand(CountryRules c, List<Transaction> sales, List<Transaction> costs)
    {
        var b5 = sales.Sum(t => t.AmountCents);
        var b8 = sales.Sum(t => t.TaxCents);
        var b6 = sales.Sum(t => ZeroRated(t, c.StandardRatePpm));
        var b7 = b5 - b6;
        var b12 = costs.Sum(t => t.TaxCents);
        var b11 = costs.Sum(t => t.AmountCents - ZeroRated(t, c.StandardRatePpm));
        var b15 = b8 - b12;
        var label = b15 >= 0 ? "GST to pay" : "GST refund";
        return ([
            new("5", "Total sales and income, including GST", b5),
            new("6", "Zero-rated supplies", b6),
            new("7", "Box 5 less box 6", b7, ReturnLine.Total),
            new("8", "GST collected (box 7 × 3/23)", b8),
            new("9", "Debit adjustments", 0, Note: NotTracked),
            new("10", "Total GST collected", b8, ReturnLine.Total),
            new("11", "Purchases and expenses with GST, including GST", b11),
            new("12", "GST credit (box 11 × 3/23)", b12),
            new("13", "Credit adjustments", 0, Note: NotTracked),
            new("14", "Total GST credit", b12, ReturnLine.Total),
            new("15", label, b15, ReturnLine.Result),
        ], b15, label);
    }

    static (List<ReturnBox>, long, string) UnitedKingdom(List<Transaction> sales, List<Transaction> costs)
    {
        var b1 = sales.Sum(t => t.TaxCents);
        var b4 = costs.Sum(t => t.TaxCents);
        var b5 = b1 - b4;
        var label = b5 >= 0 ? "VAT to pay" : "VAT to reclaim";
        return ([
            new("1", "VAT due on sales", b1),
            new("2", "VAT due on goods from the EU", 0, Note: NorthernIreland),
            new("3", "Total VAT due", b1, ReturnLine.Total),
            new("4", "VAT reclaimed on purchases", b4),
            new("5", b5 >= 0 ? "Net VAT to pay" : "Net VAT to reclaim", b5, ReturnLine.Result),
            new("6", "Total sales, excluding VAT", WholePounds(sales.Sum(t => t.ExTaxCents)), WholeUnits: true),
            new("7", "Total purchases, excluding VAT", WholePounds(costs.Sum(t => t.ExTaxCents)), WholeUnits: true),
            new("8", "Goods supplied to the EU", 0, WholeUnits: true, Note: NorthernIreland),
            new("9", "Goods bought from the EU", 0, WholeUnits: true, Note: NorthernIreland),
        ], b5, label);
    }

    // hmrc asks for boxes 6 to 9 without pence
    static long WholePounds(long cents) => cents / 100 * 100;

    static (List<ReturnBox>, long, string) Canada(List<Transaction> sales, List<Transaction> costs)
    {
        var l103 = sales.Sum(t => t.TaxCents);
        var l106 = costs.Sum(t => t.TaxCents);
        var net = l103 - l106;
        var label = net >= 0 ? "Net tax to pay" : "Net tax refund";
        return ([
            new("101", "Sales and other revenue", sales.Sum(t => t.ExTaxCents)),
            new("103", "GST/HST collected", l103),
            new("105", "Total GST/HST and adjustments", l103, ReturnLine.Total),
            new("106", "Input tax credits", l106),
            new("108", "Total input tax credits and adjustments", l106, ReturnLine.Total),
            new("109", label, net, ReturnLine.Result),
        ], net, label);
    }

    static (List<ReturnBox>, long, string) UnitedStates(List<Transaction> sales)
    {
        var total = sales.Sum(t => t.ExTaxCents);
        var taxable = sales.Where(t => t.TaxCents > 0).Sum(t => t.ExTaxCents);
        var collected = sales.Sum(t => t.TaxCents);
        return ([
            new("", "Total sales, excluding sales tax", total),
            new("", "Taxable sales", taxable),
            new("", "Non-taxable sales", total - taxable),
            new("", "Sales tax collected", collected, ReturnLine.Result),
        ], collected, "Sales tax to pay");
    }
}
