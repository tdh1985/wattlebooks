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
public sealed class EmailActions(InvoiceService invoices, ProfileService profiles, InvoicePdfExporter pdf, IMailer mailer,
    ToastService toasts, IDesktop desktop)
{
    public async Task EmailInvoiceAsync(int invoiceId, string? pdfPath = null)
    {
        var path = pdfPath ?? await pdf.ExportAsync(invoiceId);
        var invoice = await invoices.GetAsync(invoiceId) ?? throw new ValidationException("This invoice no longer exists.");
        var profile = await profiles.GetAsync();
        await ComposeAsync(invoice.Kind == InvoiceKind.Quote ? EmailTemplates.Quote(invoice, profile) : EmailTemplates.Invoice(invoice, profile), path);
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
}
