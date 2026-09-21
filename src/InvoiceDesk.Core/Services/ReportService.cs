// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed record CategoryTotal(string Category, long AmountCents, long GstCents)
{
    public long ExGstCents => AmountCents - GstCents;
}

// labels match the simpler bas: G1 total sales, 1A gst on sales, 1B gst on purchases
public sealed record BasSummary(DateRange Period, long TotalSalesCents, long GstOnSalesCents, long GstOnPurchasesCents,
    IReadOnlyList<CategoryTotal> SalesByCategory)
{
    public long NetGstCents => GstOnSalesCents - GstOnPurchasesCents;
}

public sealed record ProfitAndLoss(DateRange Period, IReadOnlyList<CategoryTotal> Income, IReadOnlyList<CategoryTotal> Expenses)
{
    public long IncomeCents => Income.Sum(c => c.ExGstCents);
    public long ExpensesCents => Expenses.Sum(c => c.ExGstCents);
    public long ProfitCents => IncomeCents - ExpensesCents;
}

// cash basis, the same way the dashboard counts it
public sealed class ReportService(IDbContextFactory<AppDbContext> factory)
{
    public const string Uncategorised = "No category";

    public async Task<BasSummary> BasAsync(DateRange period)
    {
        var txs = await LoadAsync(period);
        return new BasSummary(period,
            CashTotals.Sum(txs, Direction.In, period, t => t.AmountCents),
            CashTotals.GstCollected(txs, period),
            CashTotals.GstPaid(txs, period),
            ByCategory(txs, Direction.In));
    }

    public async Task<ProfitAndLoss> ProfitAndLossAsync(DateRange period)
    {
        var txs = await LoadAsync(period);
        return new ProfitAndLoss(period, ByCategory(txs, Direction.In), ByCategory(txs, Direction.Out));
    }

    async Task<List<Transaction>> LoadAsync(DateRange period)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions.AsNoTracking().Include(t => t.Category)
            .Where(t => t.Date >= period.Start && t.Date <= period.End)
            .ToListAsync();
    }

    // biggest first, with anything uncategorised at the end where it's easy to spot
    static List<CategoryTotal> ByCategory(IEnumerable<Transaction> txs, Direction direction) =>
        txs.Where(t => t.Direction == direction)
            .GroupBy(t => t.Category?.Name ?? Uncategorised)
            .Select(g => new CategoryTotal(g.Key, g.Sum(t => t.AmountCents), g.Sum(t => t.GstCents)))
            .OrderBy(c => c.Category == Uncategorised)
            .ThenByDescending(c => c.AmountCents)
            .ThenBy(c => c.Category)
            .ToList();
}
