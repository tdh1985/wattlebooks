// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed record StatementLine(InvoiceSummary Invoice, int DaysOverdue);

public sealed record StatementSection(string Currency, IReadOnlyList<StatementLine> Lines, AgedTotals Aged);

public sealed record Statement(Client Client, DateOnly AsOf, IReadOnlyList<StatementSection> Sections);

// everything a client still owes on one page, for chasing several invoices at once
public sealed class StatementService(IDbContextFactory<AppDbContext> factory, TimeProvider clock, ProfileService profiles)
{
    public async Task<Statement> BuildAsync(int clientId)
    {
        var asOf = clock.Today();
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clientId)
                     ?? throw new ValidationException("This client no longer exists.");
        var home = Countries.For((await profiles.GetAsync()).Country).Currency;
        var invoices = await db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(i => i.Lines).Include(i => i.Payments)
            .Where(i => i.ClientId == clientId && i.Kind == InvoiceKind.Invoice && i.Status == InvoiceStatus.Sent)
            .ToListAsync();

        var lines = invoices
            .Select(i => InvoiceSummary.From(i, asOf))
            .Where(s => s.IsAwaitingPayment)
            .OrderBy(s => s.DueDate).ThenBy(s => s.Number)
            .Select(s => new StatementLine(s with { ClientName = client.Name }, Math.Max(0, asOf.DayNumber - s.DueDate.DayNumber)))
            .ToList();
        var sections = lines
            .GroupBy(l => l.Invoice.Currency)
            .OrderBy(g => g.Key == home ? 0 : 1)
            .Select(g => new StatementSection(g.Key, g.ToList(),
                Aging.Totals(g.Select(l => (l.Invoice.DueDate, l.Invoice.BalanceCents)), asOf)))
            .ToList();
        return new Statement(client, asOf, sections);
    }
}
