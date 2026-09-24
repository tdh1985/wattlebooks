// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using InvoiceDesk.Ui.Host;
using InvoiceDesk.Ui.Pdf;
using InvoiceDesk.Ui.Platform;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;

namespace InvoiceDesk.Ui;

// the steps every email button shares: make the pdf, open the email, say what happened
public sealed class EmailActions(InvoiceService invoices, ProfileService profiles, InvoicePdfExporter pdf,
    StatementPdfExporter statementPdf, StatementService statements, IMailer mailer, ToastService toasts, IDesktop desktop)
{
    public async Task EmailInvoiceAsync(int invoiceId, string? pdfPath = null) =>
        await EmailInvoiceAsync(invoiceId, pdfPath is null ? await TryExportAsync(() => pdf.ExportAsync(invoiceId)) : new(pdfPath, null));

    async Task EmailInvoiceAsync(int invoiceId, MadePdf made)
    {
        var invoice = await invoices.GetAsync(invoiceId) ?? throw new ValidationException("This invoice no longer exists.");
        var profile = await profiles.GetAsync();
        var draft = invoice.Kind == InvoiceKind.Quote ? EmailTemplates.Quote(invoice, profile) : EmailTemplates.Invoice(invoice, profile);
        await ComposeAsync(draft, made);
    }

    // marks a draft sent, with its pdf kept when this computer can make one
    public async Task IssueAsync(int invoiceId, bool email)
    {
        var made = await TryExportAsync(() => pdf.ExportAsync(invoiceId));
        await invoices.MarkSentAsync(invoiceId, made.Path);
        if (email)
        {
            await EmailInvoiceAsync(invoiceId, made);
            return;
        }
        var number = (await invoices.GetAsync(invoiceId))?.Number;
        if (made.Path is not { } path)
        {
            toasts.Show($"{number} marked as sent. {made.WhyNot}", ToastKind.Info);
            return;
        }
        toasts.Show($"{number} marked as sent. The PDF is ready to email.", ToastKind.Success,
            new ToastAction("Open PDF", () => { desktop.OpenFile(path); return Task.CompletedTask; }),
            new ToastAction("Show in folder", () => { desktop.ShowInFolder(path); return Task.CompletedTask; }));
    }

    // recorded as soon as the email opens, with undo if it never got sent
    public async Task RemindAsync(int invoiceId, EmailDraft draft, ReminderTone tone)
    {
        var made = await TryExportAsync(() => pdf.ExportAsync(invoiceId));
        var reminder = await invoices.RecordReminderAsync(invoiceId, tone);
        await ComposeAsync(draft, made, new ToastAction("Undo reminder", () => invoices.DeleteReminderAsync(reminder.Id)));
    }

    public async Task EmailStatementAsync(int clientId)
    {
        string? path = null;
        string? whyNot = null;
        Statement statement;
        try { (path, statement) = await statementPdf.ExportAsync(clientId); }
        catch (PdfUnavailableException ex)
        {
            whyNot = ex.Message;
            statement = await statements.BuildAsync(clientId);
        }
        var owing = statement.Sections.Select(s => new CurrencyAmount(s.Currency, s.Aged.Total)).ToList();
        var draft = EmailTemplates.Statement(statement.Client, await profiles.GetAsync(), owing, statement.AsOf);
        await ComposeAsync(draft, new MadePdf(path, whyNot));
    }

    public async Task<MailOutcome> ComposeAsync(EmailDraft draft, string attachmentPath, params ToastAction[] extra)
    {
        var outcome = await mailer.ComposeAsync(draft, attachmentPath);
        var file = Path.GetFileName(attachmentPath);
        if (outcome == MailOutcome.Attached)
        {
            toasts.Show($"Your email is open with {file} attached.", ToastKind.Success, extra);
        }
        else
        {
            var show = new ToastAction("Show PDF", () => { desktop.ShowInFolder(attachmentPath); return Task.CompletedTask; });
            toasts.Show($"Your email is open. Drag {file} from the folder that just opened into it.", ToastKind.Info, [show, .. extra]);
        }
        return outcome;
    }

    readonly record struct MadePdf(string? Path, string? WhyNot);

    // nothing on this computer can make a pdf, so the email goes without one
    async Task<MadePdf> TryExportAsync(Func<Task<string>> export)
    {
        try { return new(await export(), null); }
        catch (PdfUnavailableException ex) { return new(null, ex.Message); }
    }

    async Task ComposeAsync(EmailDraft draft, MadePdf made, params ToastAction[] extra)
    {
        if (made.Path is { } path)
        {
            await ComposeAsync(draft, path, extra);
            return;
        }
        // the user attaches a pdf they print from the browser themselves
        desktop.OpenMail(Mailto.Url(draft));
        toasts.Show($"Your email is open without the PDF. {made.WhyNot}", ToastKind.Info, extra);
    }
}
