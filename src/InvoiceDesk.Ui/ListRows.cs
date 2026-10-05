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
