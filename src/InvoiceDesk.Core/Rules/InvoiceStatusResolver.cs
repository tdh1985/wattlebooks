// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

public static class InvoiceStatusResolver
{
    public static DisplayStatus Resolve(InvoiceStatus status, long totalCents, long paidCents, DateOnly dueDate, DateOnly today)
    {
        if (status == InvoiceStatus.Void) return DisplayStatus.Void;
        if (status == InvoiceStatus.Draft) return DisplayStatus.Draft;
        if (totalCents - paidCents <= 0) return DisplayStatus.Paid;
        if (today > dueDate) return DisplayStatus.Overdue;
        return paidCents > 0 ? DisplayStatus.PartPaid : DisplayStatus.Sent;
    }

    // a quote is never owed so it can't be paid or overdue
    public static DisplayStatus Resolve(InvoiceKind kind, InvoiceStatus status, long totalCents, long paidCents, DateOnly dueDate, DateOnly today)
    {
        if (kind == InvoiceKind.Invoice) return Resolve(status, totalCents, paidCents, dueDate, today);
        return status switch
        {
            InvoiceStatus.Draft => DisplayStatus.Draft,
            InvoiceStatus.Void => DisplayStatus.Void,
            InvoiceStatus.Accepted => DisplayStatus.Accepted,
            InvoiceStatus.Declined => DisplayStatus.Declined,
            _ => today > dueDate ? DisplayStatus.Expired : DisplayStatus.Sent,
        };
    }

    public static DisplayStatus Resolve(Invoice invoice, DateOnly today) =>
        Resolve(invoice.Kind, invoice.Status, invoice.Totals().TotalCents, invoice.PaidCents, invoice.DueDate, today);
}
