// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Services;

namespace InvoiceDesk.Ui;

// built once per load or filter change so a render never walks every row
public static class InvoiceRows
{
    // an undone delete must come back without asking the database again
    public static List<InvoiceSummary> Shown(IEnumerable<InvoiceSummary> loaded, Func<int, bool> isPending) =>
        loaded.Where(s => !isPending(s.Id)).ToList();

    public static List<InvoiceSummary> Filter(IEnumerable<InvoiceSummary> shown, InvoiceFilter filter, string search)
    {
        var term = search.Trim();
        return shown
            .Where(s => s.Matches(filter))
            .Where(s => term.Length == 0
                        || s.Number.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || s.ClientName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static Dictionary<InvoiceFilter, int> Counts(IReadOnlyCollection<InvoiceSummary> shown, IEnumerable<InvoiceFilter> filters) =>
        filters.Distinct().ToDictionary(f => f, f => shown.Count(s => s.Matches(f)));
}

// set from every row up front because the few rendered rows can't size them
public sealed record InvoiceColumns(int StubPx, int AmountPx, int TotalWidePx, int BalanceWidePx, int MinPx, int MinWidePx)
{
    // issued, due and status share 39% of the table and the client takes the rest
    const int SharePercent = 39;
    const int ClientMinPx = 120;
    const int ClientMinWidePx = 160;

    public static readonly InvoiceColumns Default = For([], _ => "");

    public static InvoiceColumns For(IEnumerable<InvoiceSummary> all, Func<InvoiceSummary, string> money)
    {
        int numberChars = 0, moneyChars = 9;
        foreach (var s in all)
        {
            numberChars = Math.Max(numberChars, s.Number.Length);
            moneyChars = Math.Max(moneyChars, money(s).Length);
        }
        // measured near 7.6 px a character for numbers and 6.9 px for amounts
        // 127 is the stub width at the default window before virtualising
        var stub = Math.Max(127, numberChars * 8 + 24);
        var amount = (moneyChars * 15 + 1) / 2 + 24;
        // wide windows keep the widths auto layout gave before virtualising
        var totalWide = Math.Max(138, amount);
        var balanceWide = Math.Max(166, amount);
        return new InvoiceColumns(stub, amount, totalWide, balanceWide,
            MinWidth(stub + amount + amount + ClientMinPx),
            MinWidth(stub + totalWide + balanceWide + ClientMinWidePx));
    }

    // the shares only scale, so the fixed columns and the client set the floor
    static int MinWidth(int fixedPx) => (fixedPx * 100 + (100 - SharePercent) - 1) / (100 - SharePercent);

    public string Style =>
        $"--stub:{StubPx}px;--total:{AmountPx}px;--balance:{AmountPx}px;--min:{MinPx}px;" +
        $"--total-wide:{TotalWidePx}px;--balance-wide:{BalanceWidePx}px;--min-wide:{MinWidePx}px";
}

// the strip adds up every visible row, not just the rendered ones
public sealed record MoneyRows(
    List<Transaction> Rows, long InCents, long OutCents, long InTaxCents, long OutTaxCents,
    int WithReceipts, bool ExpenseWithoutReceipt)
{
    public static readonly MoneyRows Empty = new([], 0, 0, 0, 0, 0, false);

    public static MoneyRows Build(IEnumerable<Transaction> loaded, Func<int, bool> isPending, string search)
    {
        var term = search.Trim();
        var rows = loaded
            .Where(t => !isPending(t.Id))
            .Where(t => term.Length == 0
                        || t.Party.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || t.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || t.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (t.Invoice?.Number.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        long inCents = 0, outCents = 0, inTax = 0, outTax = 0;
        var withReceipts = 0;
        var missing = false;
        foreach (var t in rows)
        {
            if (t.Direction == Direction.In)
            {
                inCents += t.AmountCents;
                inTax += t.TaxCents;
            }
            else
            {
                outCents += t.AmountCents;
                outTax += t.TaxCents;
            }
            if (t.Attachments.Count > 0) withReceipts++;
            else if (t.Direction == Direction.Out) missing = true;
        }
        return new MoneyRows(rows, inCents, outCents, inTax, outTax, withReceipts, missing);
    }
}

// a delete that leaves its undo window must reload, since the rows on screen are old
public static class PendingIds
{
    public static HashSet<int> Among(IEnumerable<int> shown, Func<int, bool> isPending) =>
        shown.Where(isPending).ToHashSet();

    // a commit and an undo both end the window, and only the database knows which
    public static bool AnyLeft(IReadOnlySet<int> before, IReadOnlySet<int> now) =>
        before.Any(id => !now.Contains(id));
}

// mixed-height rows can't be virtualised so long lists grow in pages
public sealed class RowLimit
{
    public const int Step = 200;

    public int Count { get; private set; } = Step;

    public bool HasMore(int total) => total > Count;

    public IEnumerable<T> Take<T>(IEnumerable<T> rows) => rows.Take(Count);

    public void More() => Count += Step;

    public void Reset() => Count = Step;
}
