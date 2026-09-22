// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class ClientService(IDbContextFactory<AppDbContext> factory, TimeProvider clock, ProfileService profiles)
{
    public event Action? Changed;

    public async Task<List<ClientSummary>> ListAsync(string? search = null, bool includeArchived = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Client> query = db.Clients.AsNoTracking().AsSplitQuery()
            .Include(c => c.Invoices).ThenInclude(i => i.Lines)
            .Include(c => c.Invoices).ThenInclude(i => i.Payments);
        if (!includeArchived) query = query.Where(c => !c.IsArchived);

        var clients = await query.ToListAsync();
        var term = Text.Clean(search);
        if (term.Length > 0)
            clients = clients.Where(c => Text.Has(c.Name, term) || Text.Has(c.ContactName, term) || Text.Has(c.Email, term)).ToList();

        return clients
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(Summarise)
            .ToList();
    }

    public async Task<Client?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Client> SaveAsync(Client client)
    {
        var entity = client.Clone();
        entity.Name = Text.Clean(entity.Name);
        entity.ContactName = Text.Clean(entity.ContactName);
        entity.Email = Text.Clean(entity.Email);
        entity.Phone = Text.Clean(entity.Phone);
        entity.Address = Text.Clean(entity.Address);
        entity.Notes = Text.Clean(entity.Notes);
        entity.InvoiceNote = Text.Clean(entity.InvoiceNote);
        entity.Website = Text.Clean(entity.Website);
        var profile = await profiles.GetAsync();
        entity.Country = Text.Clean(entity.Country).ToUpperInvariant();
        entity.Currency = Text.Clean(entity.Currency).ToUpperInvariant();
        var rule = InvoiceDefaults.TaxNumberRuleFor(entity, profile);
        entity.TaxNumber = rule is null ? Text.Clean(entity.TaxNumber) : rule.Normalise(Text.Clean(entity.TaxNumber));
        var country = Countries.For(InvoiceDefaults.CountryOf(entity, profile));
        // a province only matters when the client and the business are both in canada
        var sameCanada = country.Provinces.Count > 0 && !InvoiceDefaults.IsOverseas(entity, profile);
        entity.Region = sameCanada ? Text.Clean(entity.Region).ToUpperInvariant() : "";

        var errors = new List<string>();
        if (entity.Name.Length == 0) errors.Add("Client name is required.");
        if (entity.Country.Length > 0 && !WorldCountries.IsKnown(entity.Country)) errors.Add("Choose the client's country from the list.");
        if (rule is not null && entity.TaxNumber.Length > 0 && !rule.IsValid(entity.TaxNumber)) errors.Add("Client " + rule.InvalidMessage);
        if (entity.Region.Length > 0 && country.FindProvince(entity.Region) is null) errors.Add("Choose the client's province or territory from the list.");
        if (entity.Currency.Length > 0 && !Currencies.IsSupported(entity.Currency)) errors.Add("Choose a currency from the list.");
        if (entity.Website.Length > 0 && !WebAddress.IsValid(entity.Website)) errors.Add("That website doesn't look right. Use something like www.theirbusiness.com.au.");
        if (entity.Email.Length > 0 && !entity.Email.Contains('@')) errors.Add("That email address doesn't look right.");
        ValidationException.ThrowIfAny(errors);

        await using var db = await factory.CreateDbContextAsync();
        if (entity.Id == 0)
        {
            entity.CreatedAt = clock.Now();
            db.Clients.Add(entity);
        }
        else
        {
            db.Clients.Update(entity);
        }
        await db.SaveChangesAsync();
        Changed?.Invoke();
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Invoices.AnyAsync(i => i.ClientId == id))
            throw new ValidationException("This client has invoices or quotes, so it can't be deleted. Archive it instead.");

        var client = await db.Clients.FindAsync(id);
        if (client is null) return;
        db.Clients.Remove(client);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task SetArchivedAsync(int id, bool archived)
    {
        await using var db = await factory.CreateDbContextAsync();
        var client = await db.Clients.FindAsync(id);
        if (client is null) return;
        client.IsArchived = archived;
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    static ClientSummary Summarise(Client c)
    {
        var invoices = c.Invoices.Where(i => i.Kind == InvoiceKind.Invoice).ToList();
        var issued = invoices.Where(i => i.Status == InvoiceStatus.Sent).ToList();
        var billed = issued.Sum(i => i.Totals().TotalCents);
        var outstanding = issued.Sum(i => Math.Max(0, i.Totals().TotalCents - i.PaidCents));
        return new ClientSummary(c, invoices.Count, billed, outstanding, c.Invoices.Count - invoices.Count);
    }
}
