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

    public event Action? Changed;

    public async Task<BusinessProfile> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Profiles.AsNoTracking().Include(p => p.LogoAttachment).SingleAsync();
    }

    public async Task SaveAsync(BusinessProfile profile)
    {
        Normalise(profile);
        ValidationException.ThrowIfAny(Validate(profile));

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Profiles.SingleAsync();
        var logoId = existing.LogoAttachmentId;
        var country = existing.Country;
        db.Entry(existing).CurrentValues.SetValues(profile);
        // the logo only changes through SetLogoAsync so a stale form can't drop it
        existing.LogoAttachmentId = logoId;
        // the country only changes through ChangeCountryAsync so a stale form can't flip it
        existing.Country = country;
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
        if (p.TaxNumber.Length > 0 && !rules.TaxNumber.IsValid(p.TaxNumber)) errors.Add(rules.TaxNumber.InvalidMessage);
        if (p.Website.Length > 0 && !WebAddress.IsValid(p.Website)) errors.Add("That website doesn't look right. Use something like www.yourbusiness.com.au.");
        if (!HexColour().IsMatch(p.AccentColour)) errors.Add("Accent colour must look like #4F46E5.");
        if (p.BankCode.Length > 0 && rules.BankCode is { } bank && !bank.IsValid(p.BankCode)) errors.Add(bank.InvalidMessage);
        if (p.NumberPadding is < 0 or > 8) errors.Add("Number padding must be between 0 and 8.");
        if (p.NextInvoiceNumber < 1) errors.Add("Next invoice number must be 1 or more.");
        if (p.PaymentTermsDays is < 0 or > 365) errors.Add("Payment terms must be between 0 and 365 days.");
        if (p.TaxRatePpm is < 0 or > 1_000_000) errors.Add($"{rules.TaxName} rate must be between 0% and 100%.");
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
        p.AccountNumber = Text.Clean(p.AccountNumber);
        p.AccentColour = Text.Clean(p.AccentColour);
        p.InvoicePrefix = Text.Clean(p.InvoicePrefix);
        p.QuotePrefix = Text.Clean(p.QuotePrefix);
        p.FooterNote = Text.Clean(p.FooterNote);
        p.InvoiceTemplate = InvoiceTemplates.Normalise(p.InvoiceTemplate);
        p.BankCode = rules.BankCode is { } bank ? bank.Normalise(Text.Clean(p.BankCode)) : Text.Clean(p.BankCode);
        p.SwiftCode = Text.Clean(p.SwiftCode);
        p.OverseasNote = Text.Clean(p.OverseasNote);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColour();
}
