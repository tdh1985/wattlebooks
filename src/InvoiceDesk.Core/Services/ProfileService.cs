// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.RegularExpressions;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed partial class ProfileService(IDbContextFactory<AppDbContext> factory, AttachmentStore store)
{
    const string LogoTooBig = "The logo must be 5 MB or smaller.";

    public const string CountryLocked =
        "Your country is locked because you've sent invoices or recorded money. To set up a business in another country, start a new data folder in Your data.";

    public event Action? Changed;

    public async Task<BusinessProfile> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Profiles.AsNoTracking().Include(p => p.LogoAttachment).SingleAsync();
    }

    public async Task<bool> IsCountryLockedAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Invoices.AnyAsync(i => i.Status != InvoiceStatus.Draft) || await db.Transactions.AnyAsync();
    }

    // past amounts would count in another currency, so it locks once money moves
    public async Task<BusinessProfile> ChangeCountryAsync(string code)
    {
        if (!Countries.IsSupported(code)) throw new ValidationException("Choose one of the listed countries.");
        var target = Countries.For(code);

        await using var db = await factory.CreateDbContextAsync();
        var profile = await db.Profiles.SingleAsync();
        if (profile.Country == target.Code) return await GetAsync();
        if (await IsCountryLockedAsync()) throw new ValidationException(CountryLocked);

        var oldCurrency = Countries.For(profile.Country).Currency;
        target.ApplyDefaults(profile);
        var drafts = await db.Invoices.Include(i => i.Lines).Include(i => i.Client)
            .Where(i => i.Status == InvoiceStatus.Draft)
            .ToListAsync();
        foreach (var draft in drafts)
        {
            // a hand-picked overseas currency stays put when the country changes
            var keep = draft.Currency != oldCurrency ? draft.Currency : null;
            InvoiceDefaults.Apply(draft, draft.Client, profile);
            if (keep is not null) draft.Currency = keep;
            if (target.HasReducedRate) continue;
            foreach (var line in draft.Lines)
            {
                if (line.TaxCode == TaxCode.Reduced) line.TaxCode = TaxCode.Standard;
                if (line.TaxCode == TaxCode.Exempt) line.TaxCode = TaxCode.Zero;
            }
        }
        await db.SaveChangesAsync();
        Changed?.Invoke();
        return await GetAsync();
    }

    public async Task SaveAsync(BusinessProfile profile)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Profiles.SingleAsync();
        // only ChangeCountryAsync changes the country so a stale form can't flip it
        profile.Country = existing.Country;

        Normalise(profile);
        ValidationException.ThrowIfAny(Validate(profile));

        var logoId = existing.LogoAttachmentId;
        db.Entry(existing).CurrentValues.SetValues(profile);
        // the logo only changes through SetLogoAsync so a stale form can't drop it
        existing.LogoAttachmentId = logoId;
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task SetLogoAsync(StagedFile? file)
    {
        if (file is not null && !file.IsImage)
            throw new ValidationException("The logo must be a JPG, PNG or WebP image.");
        if (file is not null && new FileInfo(file.TempPath).Length > AttachmentStore.MaxLogoBytes)
            throw new ValidationException(LogoTooBig);

        await using var db = await factory.CreateDbContextAsync();
        var profile = await db.Profiles.Include(p => p.LogoAttachment).SingleAsync();
        var old = profile.LogoAttachment;

        Attachment? added = null;
        if (file is not null)
        {
            added = store.Import(file.TempPath, file.OriginalFileName, AttachmentKind.Logo);
            profile.LogoAttachment = added;
        }
        else
        {
            profile.LogoAttachment = null;
            profile.LogoAttachmentId = null;
        }
        if (old is not null) db.Attachments.Remove(old);

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            if (added is not null) store.DeleteFile(added);
            throw;
        }

        if (old is not null) store.DeleteFile(old);
        if (file is not null) store.DiscardStaged([file]);
        Changed?.Invoke();
    }

    public static List<string> Validate(BusinessProfile p)
    {
        var rules = Countries.For(p.Country);
        var errors = new List<string>();
        if (!Countries.IsSupported(p.Country)) errors.Add("Choose one of the listed countries.");
        if (p.TaxNumber.Length > 0 && !rules.TaxNumber.IsValid(p.TaxNumber)) errors.Add(rules.TaxNumber.InvalidMessage);
        if (p.Website.Length > 0 && !WebAddress.IsValid(p.Website)) errors.Add("That website doesn't look right. Use something like www.yourbusiness.com.au.");
        if (!HexColour().IsMatch(p.AccentColour)) errors.Add("Accent colour must look like #4F46E5.");
        if (p.BankCode.Length > 0 && rules.BankCode is { } bank && !bank.IsValid(p.BankCode)) errors.Add(bank.InvalidMessage);
        if (p.AccountNumber.Length > 0 && !rules.AccountIsValid(p.AccountNumber)) errors.Add(rules.AccountInvalidMessage);
        if (p.SwiftCode.Length > 0 && !NumberChecks.Swift(p.SwiftCode)) errors.Add("SWIFT/BIC must be 8 or 11 letters and numbers.");
        if (p.NumberPadding is < 0 or > 8) errors.Add("Number padding must be between 0 and 8.");
        if (p.NextInvoiceNumber < 1) errors.Add("Next invoice number must be 1 or more.");
        if (p.PaymentTermsDays is < 0 or > 365) errors.Add("Payment terms must be between 0 and 365 days.");
        if (p.TaxRatePpm is < 0 or > 1_000_000) errors.Add($"{rules.TaxName} rate must be between 0% and 100%.");
        if (p.ReducedRatePpm is < 0 or > 1_000_000) errors.Add("Reduced rate must be between 0% and 100%.");
        if (rules.Provinces.Count > 0 && rules.FindProvince(p.Region) is null) errors.Add("Choose your province or territory.");
        // the us is the only country without a national rate to start from
        if (p.TaxRegistered && p.TaxRatePpm == 0 && rules.StandardRatePpm == 0) errors.Add($"Enter the {rules.TaxWord} rate you charge.");
        if (p.TaxPeriodMonths is not (1 or 2 or 3 or 6 or 12) || p.TaxPeriodEndMonth is < 1 or > 12)
            errors.Add($"Choose how often you send your {rules.ReturnShortName}.");
        if (p.InvoicePrefix.Length > 12) errors.Add("Invoice prefix must be 12 characters or fewer.");
        if (p.QuotePrefix.Length > 12) errors.Add("Quote prefix must be 12 characters or fewer.");
        // one prefix for both would weave the two number runs together
        if (string.Equals(p.QuotePrefix, p.InvoicePrefix, StringComparison.OrdinalIgnoreCase)) errors.Add("Quotes need a different prefix from invoices.");
        if (p.NextQuoteNumber < 1) errors.Add("Next quote number must be 1 or more.");
        if (p.QuoteValidDays is < 1 or > 365) errors.Add("Quotes must be valid for between 1 and 365 days.");
        return errors;
    }

    static void Normalise(BusinessProfile p)
    {
        var rules = Countries.For(p.Country);
        p.Name = Text.Clean(p.Name);
        p.TaxNumber = rules.TaxNumber.Normalise(Text.Clean(p.TaxNumber));
        p.Address = Text.Clean(p.Address);
        p.Email = Text.Clean(p.Email);
        p.Phone = Text.Clean(p.Phone);
        p.Website = Text.Clean(p.Website);
        p.DefaultInvoiceNotes = Text.Clean(p.DefaultInvoiceNotes);
        p.PrivateNotes = Text.Clean(p.PrivateNotes);
        p.BankAccountName = Text.Clean(p.BankAccountName);
        p.AccountNumber = rules.NormaliseAccount(Text.Clean(p.AccountNumber));
        p.AccentColour = Text.Clean(p.AccentColour);
        p.InvoicePrefix = Text.Clean(p.InvoicePrefix);
        p.QuotePrefix = Text.Clean(p.QuotePrefix);
        p.FooterNote = Text.Clean(p.FooterNote);
        p.InvoiceTemplate = InvoiceTemplates.Normalise(p.InvoiceTemplate);
        p.BankCode = rules.BankCode is { } bank ? bank.Normalise(Text.Clean(p.BankCode)) : "";
        p.SwiftCode = Text.Clean(p.SwiftCode).Replace(" ", "").ToUpperInvariant();
        p.OverseasNote = Text.Clean(p.OverseasNote);
        p.Region = rules.Provinces.Count > 0 ? Text.Clean(p.Region).ToUpperInvariant() : "";
        // a canadian business charges its province's rate, so the rate isn't typed
        if (rules.FindProvince(p.Region) is { } province) p.TaxRatePpm = province.RatePpm;
        if (!rules.HasReducedRate) p.ReducedRatePpm = 0;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColour();
}
