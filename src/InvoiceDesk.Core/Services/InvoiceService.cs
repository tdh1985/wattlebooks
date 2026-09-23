// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Services;

public sealed class InvoiceService(
    IDbContextFactory<AppDbContext> factory, AttachmentStore store, TimeProvider clock,
    ProfileService profiles, CategoryService categories)
{
    public event Action? Changed;

    public async Task<Invoice> NewDraftAsync(int? clientId = null, InvoiceKind kind = InvoiceKind.Invoice)
    {
        var profile = await profiles.GetAsync();
        var today = clock.Today();
        Client? client = null;
        if (clientId is { } id)
        {
            await using var db = await factory.CreateDbContextAsync();
            client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }
        var draft = new Invoice
        {
            Kind = kind,
            ClientId = clientId ?? 0,
            IssueDate = today,
            // a quote's due date is the day its price stops holding
            DueDate = today.AddDays(kind == InvoiceKind.Quote ? profile.QuoteValidDays : profile.PaymentTermsDays),
            Status = InvoiceStatus.Draft,
            // the default notes are payment terms, which a quote doesn't have yet
            Notes = kind == InvoiceKind.Quote ? "" : profile.DefaultInvoiceNotes,
            Lines = [new InvoiceLine { Quantity = 1 }],
        };
        InvoiceDefaults.Apply(draft, client, profile);
        return draft;
    }

    public async Task<Invoice?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.AsNoTrackingWithIdentityResolution().AsSplitQuery()
            .Include(i => i.Client)
            .Include(i => i.Lines)
            .Include(i => i.Attachments)
            .Include(i => i.Payments).ThenInclude(p => p.Attachments)
            .Include(i => i.Payments).ThenInclude(p => p.Category)
            .Include(i => i.Reminders)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return null;

        inv.Lines = inv.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToList();
        inv.Payments = inv.Payments.OrderBy(p => p.Date).ThenBy(p => p.Id).ToList();
        inv.Reminders = inv.Reminders.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
        return inv;
    }

    public async Task<Reminder> RecordReminderAsync(int invoiceId, ReminderTone tone)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.FindAsync(invoiceId) ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Kind == InvoiceKind.Quote || inv.Status != InvoiceStatus.Sent) throw new ValidationException("Only sent invoices can be chased up.");

        var reminder = new Reminder { InvoiceId = invoiceId, Tone = tone, CreatedAt = clock.Now() };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();
        Changed?.Invoke();
        return reminder;
    }

    public async Task DeleteReminderAsync(int reminderId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var reminder = await db.Reminders.FindAsync(reminderId);
        if (reminder is null) return;
        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task<Invoice> SaveAsync(Invoice invoice)
    {
        var errors = new List<string>();
        if (invoice.ClientId <= 0) errors.Add("Choose a client.");
        if (invoice.DueDate < invoice.IssueDate) errors.Add("The due date can't be before the issue date.");
        if (!Currencies.IsSupported(invoice.Currency)) errors.Add("Choose a currency from the list.");
        ValidationException.ThrowIfAny(errors);

        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        Invoice entity;
        if (invoice.Id == 0)
        {
            var profile = await db.Profiles.SingleAsync();
            // set once here, since the editor never sends these back on later saves
            entity = new Invoice
            {
                CreatedAt = clock.Now(), Status = InvoiceStatus.Draft, Kind = invoice.Kind,
                RecurringScheduleId = invoice.RecurringScheduleId, ConvertedFromId = invoice.ConvertedFromId,
            };
            entity.Number = await NextNumberAsync(db, profile, invoice.Kind);
            db.Invoices.Add(entity);
        }
        else
        {
            entity = await db.Invoices.Include(i => i.Lines).Include(i => i.Payments)
                         .SingleOrDefaultAsync(i => i.Id == invoice.Id)
                     ?? throw new ValidationException("This invoice no longer exists.");
            if (entity.Status != InvoiceStatus.Draft && !string.Equals(entity.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("The currency can't change once the invoice is sent.");
            if (entity.Status == InvoiceStatus.Void) throw new ValidationException("Void invoices can't be changed.");
            if (entity.Status is InvoiceStatus.Accepted or InvoiceStatus.Declined)
                throw new ValidationException("Your client has answered this quote, so it can't be changed. Duplicate it for a new one.");
            if (entity.Payments.Count > 0)
                throw new ValidationException("This invoice has payments recorded, so it can't be changed.");
            db.InvoiceLines.RemoveRange(entity.Lines);
            entity.Lines = [];
        }

        entity.ClientId = invoice.ClientId;
        entity.IssueDate = invoice.IssueDate;
        entity.DueDate = invoice.DueDate;
        entity.TaxEnabled = invoice.TaxEnabled;
        entity.TaxRatePpm = invoice.TaxRatePpm;
        entity.ReducedRatePpm = invoice.ReducedRatePpm;
        entity.Currency = invoice.Currency.ToUpperInvariant();
        entity.Notes = Text.Clean(invoice.Notes);
        var order = 0;
        foreach (var line in invoice.Lines)
        {
            entity.Lines.Add(new InvoiceLine
            {
                SortOrder = order++,
                Description = Text.Clean(line.Description),
                Quantity = line.Quantity,
                UnitPriceCents = line.UnitPriceCents,
                TaxCode = line.TaxCode,
            });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Changed?.Invoke();
        return (await GetAsync(entity.Id))!;
    }

    public IReadOnlyList<string> ValidateForIssue(Invoice inv, BusinessProfile profile)
    {
        var errors = new List<string>();
        if (inv.ClientId <= 0) errors.Add("Choose a client.");
        if (inv.Lines.Count == 0) errors.Add("Add at least one line item.");
        for (var i = 0; i < inv.Lines.Count; i++)
        {
            var line = inv.Lines[i];
            if (string.IsNullOrWhiteSpace(line.Description)) errors.Add($"Line {i + 1} needs a description.");
            if (line.Quantity <= 0) errors.Add($"Line {i + 1} needs a quantity above zero.");
        }

        var totals = inv.Totals();
        if (totals.TotalCents < 0) errors.Add("The invoice total can't be negative.");
        if (totals.IsTaxInvoice && inv.Kind == InvoiceKind.Invoice)
        {
            var rules = Countries.For(profile.Country);
            // a tax invoice must show the seller's identity and tax number
            if (rules.TaxNumber.RequiredWhenTaxed && !rules.TaxNumber.IsValid(profile.TaxNumber)) errors.Add(rules.TaxNumber.MissingMessage);
            if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Add your business name in Settings.");
        }
        return errors;
    }

    public async Task MarkSentAsync(int id, string? pdfPath)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.Include(i => i.Lines).Include(i => i.Attachments).SingleOrDefaultAsync(i => i.Id == id)
                  ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Status == InvoiceStatus.Void) throw new ValidationException("Void invoices can't be sent.");

        var profile = await db.Profiles.AsNoTracking().SingleAsync();
        ValidationException.ThrowIfAny(ValidateForIssue(inv, profile));

        Attachment? pdf = null;
        if (pdfPath is not null)
        {
            pdf = store.Import(pdfPath, $"{inv.Number}.pdf", AttachmentKind.SentInvoicePdf);
            inv.Attachments.Add(pdf);
        }
        if (inv.Status == InvoiceStatus.Draft)
        {
            inv.Status = InvoiceStatus.Sent;
            inv.SentAt = clock.Now();
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            if (pdf is not null) store.DeleteFile(pdf);
            throw;
        }
        Changed?.Invoke();
    }

    public async Task VoidAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.FindAsync(id) ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Status == InvoiceStatus.Draft) throw new ValidationException("Drafts can be deleted instead of voided.");
        if (inv.Status == InvoiceStatus.Void) return;

        inv.Status = InvoiceStatus.Void;
        inv.VoidedAt = clock.Now();
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task DeleteDraftAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.Include(i => i.Attachments).SingleOrDefaultAsync(i => i.Id == id);
        if (inv is null) return;
        if (inv.Status != InvoiceStatus.Draft)
            throw new ValidationException("Only drafts can be deleted. Void the invoice instead.");

        var files = inv.Attachments.ToList();
        db.Invoices.Remove(inv);
        await db.SaveChangesAsync();
        // a series with nothing left in it has nothing to copy from
        if (inv.RecurringScheduleId is { } series && !await db.Invoices.AnyAsync(i => i.RecurringScheduleId == series))
            await db.RecurringSchedules.Where(s => s.Id == series).ExecuteDeleteAsync();
        store.DeleteFiles(files);
        Changed?.Invoke();
    }

    public async Task<Invoice> DuplicateAsync(int id)
    {
        var source = await GetAsync(id) ?? throw new ValidationException("This invoice no longer exists.");
        var copy = await NewDraftAsync(source.ClientId, source.Kind);
        copy.TaxEnabled = source.TaxEnabled;
        copy.TaxRatePpm = source.TaxRatePpm;
        copy.ReducedRatePpm = source.ReducedRatePpm;
        copy.Currency = source.Currency;
        copy.Notes = source.Notes;
        copy.Lines = source.Lines.Select(l => l.Copy()).ToList();
        return copy;
    }

    public Task AcceptQuoteAsync(int id) => AnswerQuoteAsync(id, InvoiceStatus.Accepted);

    public Task DeclineQuoteAsync(int id) => AnswerQuoteAsync(id, InvoiceStatus.Declined);

    async Task AnswerQuoteAsync(int id, InvoiceStatus answer)
    {
        await using var db = await factory.CreateDbContextAsync();
        var quote = await db.Invoices.FindAsync(id) ?? throw new ValidationException("This quote no longer exists.");
        if (quote.Kind != InvoiceKind.Quote) throw new ValidationException("Only quotes can be accepted or declined.");
        if (quote.Status != InvoiceStatus.Sent) throw new ValidationException("Send the quote before marking your client's answer.");

        quote.Status = answer;
        quote.AnsweredAt = clock.Now();
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    // backs out an answer marked by mistake, which is what the undo toast calls
    public async Task ReopenQuoteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var quote = await db.Invoices.FindAsync(id);
        if (quote is not { Kind: InvoiceKind.Quote, Status: InvoiceStatus.Accepted or InvoiceStatus.Declined }) return;

        quote.Status = InvoiceStatus.Sent;
        quote.AnsweredAt = null;
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task<InvoiceLink?> FindInvoiceFromQuoteAsync(int quoteId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Invoices.Where(i => i.ConvertedFromId == quoteId)
            .OrderBy(i => i.Id).Select(i => new InvoiceLink(i.Id, i.Number)).FirstOrDefaultAsync();
    }

    // the invoice starts today, since the quote's dates were about the offer
    public async Task<Invoice> TurnIntoInvoiceAsync(int quoteId)
    {
        var quote = await GetAsync(quoteId) ?? throw new ValidationException("This quote no longer exists.");
        if (quote.Kind != InvoiceKind.Quote) throw new ValidationException("Only quotes can be turned into invoices.");
        if (quote.Status == InvoiceStatus.Void) throw new ValidationException("Void quotes can't be turned into invoices.");
        if (await FindInvoiceFromQuoteAsync(quoteId) is { } made) throw new ValidationException($"This quote is already invoice {made.Number}.");

        var draft = await NewDraftAsync(quote.ClientId);
        draft.ConvertedFromId = quote.Id;
        draft.TaxEnabled = quote.TaxEnabled;
        draft.TaxRatePpm = quote.TaxRatePpm;
        draft.ReducedRatePpm = quote.ReducedRatePpm;
        draft.Currency = quote.Currency;
        draft.Lines = quote.Lines.Select(l => l.Copy()).ToList();
        var invoice = await SaveAsync(draft);

        if (quote.Status != InvoiceStatus.Accepted)
        {
            var now = clock.Now();
            await using var db = await factory.CreateDbContextAsync();
            await db.Invoices.Where(i => i.Id == quoteId).ExecuteUpdateAsync(u => u
                .SetProperty(i => i.Status, InvoiceStatus.Accepted)
                .SetProperty(i => i.AnsweredAt, now));
            Changed?.Invoke();
        }
        return invoice;
    }

    public async Task<Transaction> RecordPaymentAsync(int invoiceId, PaymentInput input, IReadOnlyList<StagedFile> receipts)
    {
        if (input.AmountCents <= 0) throw new ValidationException("Payment amount must be more than zero.");

        await using var db = await factory.CreateDbContextAsync();
        var inv = await db.Invoices.Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments)
                      .SingleOrDefaultAsync(i => i.Id == invoiceId)
                  ?? throw new ValidationException("This invoice no longer exists.");
        if (inv.Kind == InvoiceKind.Quote) throw new ValidationException("Quotes can't take payments. Turn it into an invoice first.");
        if (inv.Status != InvoiceStatus.Sent)
            throw new ValidationException(inv.Status == InvoiceStatus.Draft
                ? "Mark the invoice as sent before recording a payment."
                : "Void invoices can't take payments.");

        var home = Countries.For((await db.Profiles.AsNoTracking().SingleAsync()).Country).Currency;
        var foreign = !string.Equals(inv.Currency, home, StringComparison.OrdinalIgnoreCase);
        if (foreign && input.HomeAmountCents is not > 0)
            throw new ValidationException($"Enter the amount that reached your bank in {home}.");

        // a home invoice can be paid in usd, the aud that landed is what counts
        var paidIn = string.IsNullOrWhiteSpace(input.PaidCurrency) || string.Equals(input.PaidCurrency, inv.Currency, StringComparison.OrdinalIgnoreCase)
            ? null
            : input.PaidCurrency.Trim().ToUpperInvariant();
        if (paidIn is not null)
        {
            if (foreign) throw new ValidationException($"A payment on a {inv.Currency} invoice is recorded in {inv.Currency}.");
            if (!Currencies.IsSupported(paidIn)) throw new ValidationException("Choose a currency from the list.");
            if (input.PaidAmountCents is not > 0) throw new ValidationException($"Enter the amount they sent in {paidIn}.");
        }

        // the bank amount counts for tax and profit, the other settles the invoice
        var bankCents = foreign ? input.HomeAmountCents!.Value : input.AmountCents;
        var totals = inv.Totals();

        long shortCents = 0;
        if (input.TreatAsPaidInFull)
        {
            if (paidIn is null) throw new ValidationException("Only a payment in another currency can be treated as paid in full.");
            shortCents = totals.TotalCents - inv.PaidCents - input.AmountCents;
            if (shortCents <= 0) throw new ValidationException("This payment already covers the balance due.");
        }

        var sales = await categories.GetSalesAsync();
        var feeCategory = shortCents > 0 ? await categories.GetBankFeesAsync() : null;
        var note = Text.Clean(input.Note);
        var payment = new Transaction
        {
            Direction = Direction.In,
            Date = input.Date,
            AmountCents = bankCents,
            TaxCents = totals.IsTaxInvoice ? MoneyMath.ProportionalTax(bankCents, totals.TaxCents, totals.TotalCents) : 0,
            CategoryId = sales.Id,
            Party = inv.Client!.Name,
            Description = note.Length == 0 ? $"Payment for {inv.Number}" : $"Payment for {inv.Number} – {note}",
            InvoiceId = inv.Id,
            Method = input.Method,
            CreatedAt = clock.Now(),
            ForeignAmountCents = foreign ? input.AmountCents : paidIn is not null ? input.PaidAmountCents : null,
            ForeignCurrency = foreign ? inv.Currency : paidIn ?? "",
        };

        var files = store.ImportAll(receipts, AttachmentKind.Receipt);
        payment.Attachments.AddRange(files);
        db.Transactions.Add(payment);
        if (feeCategory is not null)
        {
            db.Transactions.Add(new Transaction
            {
                Direction = Direction.Out,
                Date = input.Date,
                AmountCents = shortCents,
                TaxCents = 0,
                CategoryId = feeCategory.Id,
                Party = inv.Client.Name,
                Description = $"Bank fee on {inv.Number}",
                InvoiceId = inv.Id,
                Method = PaymentMethod.None,
                CreatedAt = clock.Now(),
            });
        }
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            store.DeleteFiles(files);
            throw;
        }

        store.DiscardStaged(receipts);
        payment.Category = sales;
        Changed?.Invoke();
        return payment;
    }

    public async Task<List<InvoiceSummary>> ListAsync(
        InvoiceFilter filter = InvoiceFilter.All, string? search = null, int? clientId = null, InvoiceKind kind = InvoiceKind.Invoice)
    {
        await using var db = await factory.CreateDbContextAsync();
        IQueryable<Invoice> query = db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(i => i.Client).Include(i => i.Lines).Include(i => i.Payments)
            .Where(i => i.Kind == kind);
        if (clientId is { } cid) query = query.Where(i => i.ClientId == cid);

        var today = clock.Today();
        var term = Text.Clean(search);
        return (await query.ToListAsync())
            .Select(i => InvoiceSummary.From(i, today))
            .Where(s => s.Matches(filter))
            .Where(s => term.Length == 0 || Text.Has(s.Number, term) || Text.Has(s.ClientName, term))
            .OrderByDescending(s => s.IssueDate)
            .ThenByDescending(s => s.Id)
            .ToList();
    }

    // skip used numbers so lowering the counter can't create duplicates
    static async Task<string> NextNumberAsync(AppDbContext db, BusinessProfile profile, InvoiceKind kind)
    {
        var used = (await db.Invoices.Select(i => i.Number).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var quote = kind == InvoiceKind.Quote;
        var prefix = quote ? profile.QuotePrefix : profile.InvoicePrefix;
        var n = Math.Max(1, quote ? profile.NextQuoteNumber : profile.NextInvoiceNumber);
        string number;
        while (used.Contains(number = InvoiceNumbering.Format(prefix, n, profile.NumberPadding))) n++;
        if (quote) profile.NextQuoteNumber = n + 1;
        else profile.NextInvoiceNumber = n + 1;
        return number;
    }
}
