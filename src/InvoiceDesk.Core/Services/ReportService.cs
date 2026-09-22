// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed record CategoryTotal(string Category, long AmountCents, long TaxCents)
{
    public long ExTaxCents => AmountCents - TaxCents;
}

public sealed record ProfitAndLoss(DateRange Period, IReadOnlyList<CategoryTotal> Income, IReadOnlyList<CategoryTotal> Expenses)
{
    public long IncomeCents => Income.Sum(c => c.ExTaxCents);
    public long ExpensesCents => Expenses.Sum(c => c.ExTaxCents);
    public long ProfitCents => IncomeCents - ExpensesCents;
}

// cash basis, the same way the dashboard counts it
public sealed class ReportService(IDbContextFactory<AppDbContext> factory, ProfileService profiles)
{
    public const string Uncategorised = "No category";

    public async Task<TaxReturn> TaxReturnAsync(DateRange period)
    {
        var txs = await LoadAsync(period);
        var country = Countries.For((await profiles.GetAsync()).Country);
        return TaxReturns.Build(country, period, txs, ByCategory(txs, Direction.In));
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
            .Select(g => new CategoryTotal(g.Key, g.Sum(t => t.AmountCents), g.Sum(t => t.TaxCents)))
            .OrderBy(c => c.Category == Uncategorised)
            .ThenByDescending(c => c.AmountCents)
            .ThenBy(c => c.Category)
            .ToList();
}
