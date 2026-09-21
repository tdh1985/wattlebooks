// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class SearchService(IDbContextFactory<AppDbContext> factory, TimeProvider clock)
{
    const int PerKind = 5;
    const int MaxResults = 12;

    public async Task<List<SearchResult>> SearchAsync(string? query)
    {
        var term = Text.Clean(query);
        if (term.Length == 0) return [];

        await using var db = await factory.CreateDbContextAsync();
        var clients = (await db.Clients.AsNoTracking().ToListAsync())
            .Where(c => Text.Has(c.Name, term) || Text.Has(c.ContactName, term) || Text.Has(c.Email, term))
            .OrderBy(c => c.IsArchived).ThenBy(c => c.Name)
            .Take(PerKind)
            .Select(c => new SearchResult(SearchKind.Client, c.Id, c.Name, c.ContactName.Length > 0 ? c.ContactName : c.Email));

        var today = clock.Today();
        var documents = (await db.Invoices.AsNoTracking().AsSplitQuery()
                .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments).ToListAsync())
            .Where(i => Text.Has(i.Number, term) || Text.Has(i.Client?.Name, term))
            .OrderByDescending(i => i.IssueDate)
            .ToList();

        // kept in two runs so the palette shows each heading once
        IEnumerable<SearchResult> Documents(InvoiceKind kind, SearchKind shownAs) => documents
            .Where(i => i.Kind == kind)
            .Take(PerKind)
            .Select(i => InvoiceSummary.From(i, today))
            .Select(s => new SearchResult(shownAs, s.Id, s.Number, s.ClientName, s.TotalCents, s.IssueDate, s.Status, s.BalanceCents));

        var txs = (await db.Transactions.AsNoTracking().ToListAsync())
            .Where(t => Text.Has(t.Party, term) || Text.Has(t.Description, term))
            .OrderByDescending(t => t.Date)
            .Take(PerKind)
            .Select(t => new SearchResult(SearchKind.Transaction, t.Id, t.Party.Length > 0 ? t.Party : t.Description,
                t.Direction == Direction.In ? "Money in" : "Money out", t.AmountCents, t.Date));

        return clients
            .Concat(Documents(InvoiceKind.Invoice, SearchKind.Invoice))
            .Concat(Documents(InvoiceKind.Quote, SearchKind.Quote))
            .Concat(txs)
            .Take(MaxResults).ToList();
    }
}
