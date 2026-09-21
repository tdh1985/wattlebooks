// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text;
using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

public sealed record EmailDraft(string To, string Subject, string Body);

// ready-to-send wording, so emailing an invoice is one click and a quick read
public static class EmailTemplates
{
    static readonly CultureInfo Au = CultureInfo.GetCultureInfo("en-AU");
    static readonly CultureInfo Months = CultureInfo.InvariantCulture;

    public static ReminderTone ToneFor(int remindersSent) => remindersSent switch
    {
        0 => ReminderTone.Polite,
        1 => ReminderTone.Firm,
        _ => ReminderTone.Final,
    };

    public static EmailDraft Invoice(Invoice inv, BusinessProfile profile)
    {
        var totals = inv.Totals();
        var kind = totals.IsTaxInvoice ? "Tax invoice" : "Invoice";
        var body = new StringBuilder()
            .Append(Greeting(inv.Client)).Append("\n\n")
            .Append($"Please find attached invoice {inv.Number} for {Money(totals.TotalCents)}, due on {Date(inv.DueDate)}.\n\n")
            .Append(PaymentBlock(profile, inv.Number))
            .Append(SignOff(profile));
        return new EmailDraft(inv.Client?.Email ?? "", $"{kind} {inv.Number} from {BusinessName(profile)}", body.ToString());
    }

    // no bank details, since nothing is owed until the quote becomes an invoice
    public static EmailDraft Quote(Invoice quote, BusinessProfile profile)
    {
        var body = new StringBuilder()
            .Append(Greeting(quote.Client)).Append("\n\n")
            .Append($"Please find attached quote {quote.Number} for {Money(quote.Totals().TotalCents)}. It's valid until {Date(quote.DueDate)}.\n\n")
            .Append("If you'd like to go ahead, just reply to this email. Happy to answer any questions too.\n\n")
            .Append(SignOff(profile));
        return new EmailDraft(quote.Client?.Email ?? "", $"Quote {quote.Number} from {BusinessName(profile)}", body.ToString());
    }

    public static EmailDraft Reminder(Invoice inv, BusinessProfile profile, DateOnly today, ReminderTone tone)
    {
        var total = inv.Totals().TotalCents;
        var balance = total - inv.PaidCents;
        var partPaid = balance < total;
        var due = Date(inv.DueDate);
        var late = today.DayNumber - inv.DueDate.DayNumber;
        var overdue = late == 1 ? "1 day overdue" : $"{late} days overdue";

        // part paid invoices ask for what's left, so the sentence has to change shape
        var nowOverdue = partPaid
            ? $"Invoice {inv.Number} still has {Money(balance)} owing and is now {overdue}. It was due on {due}."
            : $"Invoice {inv.Number} for {Money(balance)} is now {overdue}. It was due on {due}.";
        var (subject, message) = tone switch
        {
            ReminderTone.Polite => (
                $"Reminder: invoice {inv.Number}",
                (partPaid
                    ? $"Just a friendly reminder that invoice {inv.Number} still has {Money(balance)} owing. It was due on {due}. "
                    : $"Just a friendly reminder that invoice {inv.Number} for {Money(balance)} was due on {due}. ") +
                "If you've already paid, thank you, and please ignore this email. I've attached the invoice again in case it's handy."),
            ReminderTone.Firm => (
                $"Overdue: invoice {inv.Number} is {overdue}",
                nowOverdue + " Could you please arrange payment this week? If there's a problem with the invoice, let me know and I'll sort it out."),
            _ => (
                $"Final reminder: invoice {inv.Number} is {overdue}",
                nowOverdue + " Please pay the balance within 7 days. If you can't pay it all at once, get in touch so we can agree on a plan."),
        };

        var body = new StringBuilder()
            .Append(Greeting(inv.Client)).Append("\n\n")
            .Append(message).Append("\n\n")
            .Append(PaymentBlock(profile, inv.Number))
            .Append(SignOff(profile));
        return new EmailDraft(inv.Client?.Email ?? "", subject, body.ToString());
    }

    public static EmailDraft Statement(Client client, BusinessProfile profile, long totalDueCents, DateOnly asOf)
    {
        var body = new StringBuilder()
            .Append(Greeting(client)).Append("\n\n")
            .Append($"I've attached a statement of your account as at {Date(asOf)}. ")
            .Append(totalDueCents > 0 ? $"The total owing is {Money(totalDueCents)}.\n\n" : "Everything is paid, thank you.\n\n")
            .Append(totalDueCents > 0 ? PaymentBlock(profile, "the invoice numbers") : "")
            .Append(SignOff(profile));
        return new EmailDraft(client.Email, $"Statement from {BusinessName(profile)}", body.ToString());
    }

    static string Greeting(Client? client)
    {
        var first = client?.ContactName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is null ? "Hello," : $"Hi {first},";
    }

    static string PaymentBlock(BusinessProfile profile, string reference)
    {
        if (string.IsNullOrWhiteSpace(profile.Bsb) || string.IsNullOrWhiteSpace(profile.AccountNumber)) return "";
        var block = new StringBuilder("You can pay by bank transfer to:\n");
        if (!string.IsNullOrWhiteSpace(profile.BankAccountName)) block.Append($"Account name: {profile.BankAccountName.Trim()}\n");
        block.Append($"BSB: {profile.Bsb.Trim()}\n")
            .Append($"Account number: {profile.AccountNumber.Trim()}\n")
            .Append($"Reference: {reference}\n\n");
        return block.ToString();
    }

    static string SignOff(BusinessProfile profile) => $"Thanks,\n{BusinessName(profile)}";

    static string BusinessName(BusinessProfile profile) => profile.Name.Trim() is { Length: > 0 } name ? name : "us";

    static string Money(long cents) => (cents / 100m).ToString("C2", Au);

    static string Date(DateOnly d) => d.ToString("d MMMM yyyy", Months);
}
