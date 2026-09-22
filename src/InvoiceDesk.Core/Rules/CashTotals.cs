// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

// cash basis sums shared by the dashboard and the reports so they always agree
public static class CashTotals
{
    public static long Sum(IEnumerable<Transaction> txs, Direction direction, DateRange range, Func<Transaction, long> pick) =>
        txs.Where(t => t.Direction == direction && range.Contains(t.Date)).Sum(pick);

    public static long TaxCollected(IEnumerable<Transaction> txs, DateRange range) =>
        Sum(txs, Direction.In, range, t => t.TaxCents);

    public static long TaxPaid(IEnumerable<Transaction> txs, DateRange range) =>
        Sum(txs, Direction.Out, range, t => t.TaxCents);
}
