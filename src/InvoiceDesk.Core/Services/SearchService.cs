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

    sealed record ClientRow(int Id, string Name, string ContactName, string Email, bool IsArchived);
    sealed record DocumentRow(int Id, InvoiceKind Kind, string Number, string ClientName, DateOnly IssueDate);
    sealed record TxRow(int Id, string Party, string Description, DateOnly Date, Direction Direction, long AmountCents);

    public async Task<List<SearchResult>> SearchAsync(string? query)
    {
        var term = Text.Clean(query);
        if (term.Length == 0) return [];

        // sql only narrows ascii terms, the rest need the exact in-memory check
        var pattern = LikePattern(term);

        await using var db = await factory.CreateDbContextAsync();
        var clientRows = db.Clients.AsNoTracking();
        if (pattern is not null)
            clientRows = clientRows.Where(c => EF.Functions.Like(c.Name, pattern, "\\")
                || EF.Functions.Like(c.ContactName, pattern, "\\") || EF.Functions.Like(c.Email, pattern, "\\"));
        var clients = (await clientRows.OrderBy(c => c.Id)
                .Select(c => new ClientRow(c.Id, c.Name, c.ContactName, c.Email, c.IsArchived)).ToListAsync())
            .Where(c => Text.Has(c.Name, term) || Text.Has(c.ContactName, term) || Text.Has(c.Email, term))
            .OrderBy(c => c.IsArchived).ThenBy(c => c.Name)
            .Take(PerKind)
            .Select(c => new SearchResult(SearchKind.Client, c.Id, c.Name, c.ContactName.Length > 0 ? c.ContactName : c.Email))
            .ToList();

        var documentRows = db.Invoices.AsNoTracking();
        if (pattern is not null)
            documentRows = documentRows.Where(i => EF.Functions.Like(i.Number, pattern, "\\")
                || EF.Functions.Like(i.Client!.Name, pattern, "\\"));
        var documents = (await documentRows.OrderBy(i => i.Id)
                .Select(i => new DocumentRow(i.Id, i.Kind, i.Number, i.Client!.Name, i.IssueDate)).ToListAsync())
            .Where(i => Text.Has(i.Number, term) || Text.Has(i.ClientName, term))
            .OrderByDescending(i => i.IssueDate)
            .ToList();

        // kept in two runs so the palette shows each heading once
        var invoiceIds = documents.Where(i => i.Kind == InvoiceKind.Invoice).Take(PerKind).Select(i => i.Id).ToList();
        var quoteIds = documents.Where(i => i.Kind == InvoiceKind.Quote).Take(PerKind).Select(i => i.Id).ToList();
        var picked = invoiceIds.Concat(quoteIds).ToList();
        var loaded = picked.Count == 0 ? []
            : await db.Invoices.AsNoTracking().AsSplitQuery()
                .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments)
                .Where(i => picked.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        var today = clock.Today();
        IEnumerable<SearchResult> Documents(List<int> ids, SearchKind shownAs) => ids
            .Select(id => InvoiceSummary.From(loaded[id], today))
            .Select(s => new SearchResult(shownAs, s.Id, s.Number, s.ClientName, s.TotalCents, s.IssueDate, s.Status, s.BalanceCents, s.Currency));

        var txRows = db.Transactions.AsNoTracking();
        if (pattern is not null)
            txRows = txRows.Where(t => EF.Functions.Like(t.Party, pattern, "\\") || EF.Functions.Like(t.Description, pattern, "\\"));
        var txs = (await txRows.OrderBy(t => t.Id)
                .Select(t => new TxRow(t.Id, t.Party, t.Description, t.Date, t.Direction, t.AmountCents)).ToListAsync())
            .Where(t => Text.Has(t.Party, term) || Text.Has(t.Description, term))
            .OrderByDescending(t => t.Date)
            .Take(PerKind)
            .Select(t => new SearchResult(SearchKind.Transaction, t.Id, t.Party.Length > 0 ? t.Party : t.Description,
                t.Direction == Direction.In ? "Money in" : "Money out", t.AmountCents, t.Date));

        return clients
            .Concat(Documents(invoiceIds, SearchKind.Invoice))
            .Concat(Documents(quoteIds, SearchKind.Quote))
            .Concat(txs)
            .Take(MaxResults).ToList();
    }

    // like folds ascii case only, so other terms need the in-memory check
    static string? LikePattern(string term)
    {
        if (term.Any(ch => ch > 127)) return null;
        var text = new System.Text.StringBuilder("%");
        foreach (var ch in term)
        {
            if (ch is '%' or '_' or '\\') text.Append('\\').Append(ch);
            else text.Append(ch);
        }
        return text.Append('%').ToString();
    }
}
