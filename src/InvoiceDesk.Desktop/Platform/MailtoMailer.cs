// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Rules;
using InvoiceDesk.Ui.Platform;

namespace InvoiceDesk.Desktop.Platform;

// mail apps off windows have no common way to take an attachment, so the pdf is shown to drag in
public sealed class MailtoMailer(IDesktop desktop) : IMailer
{
    public Task<MailOutcome> ComposeAsync(EmailDraft draft, string attachmentPath)
    {
        desktop.OpenMail(Mailto.Url(draft));
        desktop.ShowInFolder(attachmentPath);
        return Task.FromResult(MailOutcome.OpenedWithoutAttachment);
    }
}
