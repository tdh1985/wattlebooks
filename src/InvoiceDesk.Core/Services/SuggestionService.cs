// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed record LineSuggestion(string Description, long UnitPriceCents, TaxCode TaxCode, bool ForThisClient);

// suggestions come only from what was already typed, nothing leaves the pc
public sealed class SuggestionService(IDbContextFactory<AppDbContext> factory)
{
    const int MinTermLength = 2;

    public async Task<List<LineSuggestion>> LineSuggestionsAsync(int? clientId, string? term, int take = 6)
    {
        var t = Text.Clean(term);
        if (t.Length < MinTermLength) return [];

        await using var db = await factory.CreateDbContextAsync();
        // only sent invoices, drafts are full of half-typed lines
        var lines = await db.InvoiceLines.AsNoTracking()
            .Join(db.Invoices.Where(i => i.Status == InvoiceStatus.Sent), l => l.InvoiceId, i => i.Id, (l, i) => new
            {
                l.Description, l.UnitPriceCents, l.TaxCode, i.ClientId, i.IssueDate, InvoiceId = i.Id, l.SortOrder,
            })
            .ToListAsync();

        return lines
            .Where(l => Text.Has(l.Description, t))
            .GroupBy(l => l.Description.Trim().ToLowerInvariant())
            .Select(g =>
            {
                var ours = g.Where(l => l.ClientId == clientId).ToList();
                var pick = (ours.Count > 0 ? ours : g.ToList())
                    .OrderByDescending(l => l.IssueDate).ThenByDescending(l => l.InvoiceId).ThenBy(l => l.SortOrder)
                    .First();
                return (Pick: pick, Suggestion: new LineSuggestion(pick.Description, pick.UnitPriceCents, pick.TaxCode, ours.Count > 0));
            })
            .OrderByDescending(x => x.Suggestion.ForThisClient)
            .ThenByDescending(x => x.Pick.IssueDate).ThenByDescending(x => x.Pick.InvoiceId)
            .Take(take)
            .Select(x => x.Suggestion)
            .ToList();
    }

    public async Task<int?> SuggestCategoryAsync(Direction direction, string? party)
    {
        var p = Text.Clean(party);
        if (p.Length == 0) return null;

        await using var db = await factory.CreateDbContextAsync();
        var past = await db.Transactions.AsNoTracking()
            .Where(t => t.Direction == direction && t.CategoryId != null && !t.Category!.IsArchived)
            .Select(t => new { t.Party, t.CategoryId, t.Date, t.Id })
            .ToListAsync();

        return past
            .Where(t => string.Equals(t.Party.Trim(), p, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Select(t => t.CategoryId)
            .FirstOrDefault();
    }

    public async Task<List<string>> PartiesAsync(Direction direction, int take = 50)
    {
        await using var db = await factory.CreateDbContextAsync();
        var past = await db.Transactions.AsNoTracking()
            .Where(t => t.Direction == direction && t.Party != "")
            .Select(t => new { t.Party, t.Date, t.Id })
            .ToListAsync();

        return past
            .GroupBy(t => t.Party.Trim().ToLowerInvariant())
            .Select(g => g.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).First())
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Take(take)
            .Select(t => t.Party.Trim())
            .ToList();
    }
}
