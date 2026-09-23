// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Ui.Platform;

public enum MailOutcome
{
    // the email opened with the pdf already attached
    Attached,
    // the email opened but the pdf has to be dragged in by hand
    OpenedWithoutAttachment,
}

// hands a ready email to whatever mail app the pc uses, nothing is sent from here
public interface IMailer
{
    Task<MailOutcome> ComposeAsync(EmailDraft draft, string attachmentPath);
}
