// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed record ExpectedPayment(InvoiceSummary Invoice, DateOnly ExpectedDate);

// the home figures leave out foreign invoices, which Other lists on their own
public sealed record CashForecast(
    long TotalCents, int Days, IReadOnlyList<ExpectedPayment> Items, IReadOnlyList<CurrencyAmount> Other,
    int HomeCount, long HomeOverdueCents);

public sealed class InsightService(IDbContextFactory<AppDbContext> factory, TimeProvider clock, ProfileService profiles)
{
    public async Task<PaymentHabit?> GetClientHabitAsync(int clientId)
    {
        var invoices = await LoadSentAsync(clientId);
        return Habit(invoices);
    }

    public async Task<CashForecast> GetForecastAsync(int days = 30)
    {
        var today = clock.Today();
        var home = Countries.For((await profiles.GetAsync()).Country).Currency;
        var invoices = await LoadSentAsync(clientId: null);
        var habits = invoices.GroupBy(i => i.ClientId).ToDictionary(g => g.Key, g => Habit(g));

        var items = invoices
            .Select(i => InvoiceSummary.From(i, today))
            .Where(s => s.IsAwaitingPayment)
            .Select(s => new ExpectedPayment(s, PaymentHabits.ExpectedDate(s.DueDate, habits[s.ClientId], today)))
            .Where(e => e.ExpectedDate <= today.AddDays(days))
            .OrderBy(e => e.ExpectedDate).ThenBy(e => e.Invoice.Number)
            .ToList();
        var other = items.Where(e => e.Invoice.Currency != home)
            .GroupBy(e => e.Invoice.Currency)
            .Select(g => new CurrencyAmount(g.Key, g.Sum(e => e.Invoice.BalanceCents)))
            .OrderBy(c => c.Currency, StringComparer.Ordinal)
            .ToList();
        var atHome = items.Where(e => e.Invoice.Currency == home).ToList();
        var overdue = atHome.Where(e => e.Invoice.Status == DisplayStatus.Overdue).Sum(e => e.Invoice.BalanceCents);
        return new CashForecast(atHome.Sum(e => e.Invoice.BalanceCents), days, items, other, atHome.Count, overdue);
    }

    async Task<List<Invoice>> LoadSentAsync(int? clientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Invoice> query = db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments)
            .Where(i => i.Kind == InvoiceKind.Invoice && i.Status == InvoiceStatus.Sent);
        if (clientId is { } id) query = query.Where(i => i.ClientId == id);
        return await query.ToListAsync();
    }

    static PaymentHabit? Habit(IEnumerable<Invoice> invoices) =>
        PaymentHabits.From(invoices
            .Select(i => (i.DueDate, Paid: PaymentHabits.PaidDate(i.Totals().TotalCents,
                i.Payments.Where(p => p.Direction == Direction.In).Select(p => (p.Date, i.SettledBy(p))))))
            .Where(x => x.Paid is not null)
            .Select(x => (x.DueDate, x.Paid!.Value)));
}
