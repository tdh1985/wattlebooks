// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class DashboardService(IDbContextFactory<AppDbContext> factory, TimeProvider clock, ProfileService profiles)
{
    const int RecentCount = 8;

    public async Task<DashboardData> GetAsync()
    {
        var today = clock.Today();
        await using var db = await factory.CreateDbContextAsync();
        var invoices = await db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments)
            .ToListAsync();
        var txs = await db.Transactions.AsNoTracking().Include(t => t.Invoice).ToListAsync();

        var summaries = invoices.Where(i => i.Kind == InvoiceKind.Invoice).Select(i => InvoiceSummary.From(i, today)).ToList();
        var open = summaries.Where(s => s.IsAwaitingPayment).ToList();
        var overdue = open.Where(s => s.Status == DisplayStatus.Overdue).OrderBy(s => s.DueDate).ToList();

        var profile = await profiles.GetAsync();
        var country = Countries.For(profile.Country);
        var month = FinancialPeriods.Month(today);
        var year = FinancialPeriods.TaxYear(today, country);
        var period = FinancialPeriods.ReturnPeriod(today, profile.TaxPeriodMonths, profile.TaxPeriodEndMonth);

        long Sum(Direction d, DateRange r, Func<Transaction, long> pick) => CashTotals.Sum(txs, d, r, pick);

        var months = Enumerable.Range(0, 12)
            .Select(i => FinancialPeriods.Month(month.Start.AddMonths(i - 11)))
            .Select(r => new MonthBar(r.Start, Sum(Direction.In, r, t => t.AmountCents), Sum(Direction.Out, r, t => t.AmountCents)))
            .ToList();

        return new DashboardData(
            OutstandingCents: open.Sum(s => s.BalanceCents),
            OutstandingCount: open.Count,
            OverdueCents: overdue.Sum(s => s.BalanceCents),
            OverdueCount: overdue.Count,
            ReceivedMonthCents: Sum(Direction.In, month, t => t.AmountCents),
            SpentMonthCents: Sum(Direction.Out, month, t => t.AmountCents),
            ProfitYearCents: Sum(Direction.In, year, t => t.ExTaxCents) - Sum(Direction.Out, year, t => t.ExTaxCents),
            TaxCollectedPeriodCents: CashTotals.TaxCollected(txs, period),
            TaxPaidPeriodCents: CashTotals.TaxPaid(txs, period),
            YearLabel: FinancialPeriods.TaxYearLabel(today, country),
            PeriodLabel: FinancialPeriods.ReturnPeriodLabel(period, country),
            Months: months,
            Overdue: overdue,
            Recent: Recent(invoices, txs));
    }

    static List<ActivityItem> Recent(List<Invoice> invoices, List<Transaction> txs)
    {
        var items = new List<ActivityItem>();
        foreach (var inv in invoices)
        {
            var client = inv.Client?.Name ?? "";
            var total = inv.Totals().TotalCents;
            // a draft in a series was made by the app, not typed, so it says so
            var made = inv is { RecurringScheduleId: not null, Status: InvoiceStatus.Draft } ? "drafted to repeat" : "created";
            items.Add(new ActivityItem(inv.CreatedAt, ActivityKind.InvoiceCreated, inv.Id, $"{inv.Number} {made}", client, total));
            if (inv.SentAt is { } sent)
                items.Add(new ActivityItem(sent, ActivityKind.InvoiceSent, inv.Id, $"{inv.Number} sent", client, total));
        }

        foreach (var t in txs)
        {
            var label = t.Party.Length > 0 ? t.Party : t.Description;
            items.Add(t switch
            {
                { InvoiceId: { } invoiceId } => new ActivityItem(t.CreatedAt, ActivityKind.PaymentReceived, invoiceId,
                    $"Payment from {t.Party}", t.Invoice?.Number ?? "", t.AmountCents),
                { Direction: Direction.In } => new ActivityItem(t.CreatedAt, ActivityKind.Income, t.Id, label, "Income", t.AmountCents),
                _ => new ActivityItem(t.CreatedAt, ActivityKind.Expense, t.Id, label, "Expense", t.AmountCents),
            });
        }

        return items.OrderByDescending(i => i.At).Take(RecentCount).ToList();
    }
}
