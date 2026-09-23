// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class TransactionService(
    IDbContextFactory<AppDbContext> factory, AttachmentStore store, TimeProvider clock, ProfileService profiles)
{
    public const string BankFeePrefix = "Bank fee on ";

    public event Action? Changed;

    public async Task<List<Transaction>> ListAsync(TransactionFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Transaction> query = db.Transactions.AsNoTracking().AsSplitQuery()
            .Include(t => t.Category).Include(t => t.Attachments).Include(t => t.Invoice);
        if (filter.Direction is { } d) query = query.Where(t => t.Direction == d);
        if (filter.Range is { } r) query = query.Where(t => t.Date >= r.Start && t.Date <= r.End);
        if (filter.CategoryId is { } c) query = query.Where(t => t.CategoryId == c);

        var term = Text.Clean(filter.Search);
        return (await query.ToListAsync())
            .Where(t => term.Length == 0 || Text.Has(t.Party, term) || Text.Has(t.Description, term) || Text.Has(t.Notes, term)
                        || Text.Has(t.Category?.Name, term) || Text.Has(t.Invoice?.Number, term))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToList();
    }

    public async Task<Transaction?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Transactions.AsNoTracking().AsSplitQuery()
            .Include(t => t.Category).Include(t => t.Attachments).Include(t => t.Invoice)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Transaction> SaveAsync(Transaction t, IReadOnlyList<StagedFile> newFiles, IReadOnlyList<int> removedAttachmentIds)
    {
        var rules = Countries.For((await profiles.GetAsync()).Country);
        if (t.Direction == Direction.Out && !rules.ExpensesCarryTax) t.TaxCents = 0;

        // home money never carries a foreign side, so stray values are dropped
        var foreign = !string.IsNullOrWhiteSpace(t.ForeignCurrency) && !string.Equals(t.ForeignCurrency.Trim(), rules.Currency, StringComparison.OrdinalIgnoreCase);
        if (!foreign)
        {
            t.ForeignCurrency = "";
            t.ForeignAmountCents = null;
        }

        var errors = new List<string>();
        if (t.AmountCents <= 0)
            errors.Add(!foreign ? "Amount must be more than zero."
                : t.Direction == Direction.Out ? $"Enter the amount that left your bank in {rules.Currency}."
                : $"Enter the amount that reached your bank in {rules.Currency}.");
        if (foreign)
        {
            if (Currencies.Find(t.ForeignCurrency.Trim()) is { } known) t.ForeignCurrency = known.Code;
            else errors.Add("Choose a currency from the list.");
            if (t.ForeignAmountCents is not > 0) errors.Add($"Enter the amount charged in {t.ForeignCurrency.Trim().ToUpperInvariant()}.");
        }
        if (t.TaxCents < 0 || t.TaxCents > t.AmountCents) errors.Add($"{rules.TaxName} must be between zero and the amount.");
        if (t.Date == default) errors.Add("Choose a date.");
        ValidationException.ThrowIfAny(errors);

        await using var db = await factory.CreateDbContextAsync();
        Transaction entity;
        if (t.Id == 0)
        {
            entity = new Transaction { CreatedAt = clock.Now() };
            db.Transactions.Add(entity);
        }
        else
        {
            entity = await db.Transactions.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == t.Id)
                     ?? throw new ValidationException("This entry no longer exists.");
        }

        entity.Direction = t.Direction;
        entity.Date = t.Date;
        entity.AmountCents = t.AmountCents;
        entity.TaxCents = t.TaxCents;
        entity.CategoryId = t.CategoryId;
        entity.Party = Text.Clean(t.Party);
        entity.Description = Text.Clean(t.Description);
        entity.Notes = Text.Clean(t.Notes);
        entity.Method = t.Method;
        entity.InvoiceId = t.InvoiceId;
        entity.ForeignAmountCents = t.ForeignAmountCents;
        entity.ForeignCurrency = t.ForeignCurrency;

        var removed = entity.Attachments.Where(a => removedAttachmentIds.Contains(a.Id)).ToList();
        foreach (var a in removed)
        {
            entity.Attachments.Remove(a);
            db.Attachments.Remove(a);
        }

        var added = store.ImportAll(newFiles, AttachmentKind.Receipt);
        entity.Attachments.AddRange(added);
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            store.DeleteFiles(added);
            throw;
        }

        store.DeleteFiles(removed);
        store.DiscardStaged(newFiles);
        Changed?.Invoke();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.Transactions.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id);
        if (entity is null) return;

        var doomed = new List<Transaction> { entity };
        if (entity is { Direction: Direction.In, InvoiceId: { } invoiceId })
        {
            // a grossed-up payment would leave its fee as money that never left the bank
            doomed.AddRange(await db.Transactions.Include(x => x.Attachments)
                .Where(x => x.InvoiceId == invoiceId && x.Direction == Direction.Out && x.Date == entity.Date
                            && x.Description.StartsWith(BankFeePrefix))
                .ToListAsync());
        }

        var files = doomed.SelectMany(x => x.Attachments).ToList();
        db.Transactions.RemoveRange(doomed);
        await db.SaveChangesAsync();
        store.DeleteFiles(files);
        Changed?.Invoke();
    }

    public long SuggestTax(long amountCents, BusinessProfile profile, Direction direction) =>
        profile.TaxRegistered && (direction == Direction.In || Countries.For(profile.Country).ExpensesCarryTax)
            ? MoneyMath.TaxFromInclusive(amountCents, profile.TaxRatePpm)
            : 0;
}
