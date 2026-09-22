// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public class Invoice
{
    public int Id { get; set; }
    public InvoiceKind Kind { get; set; }
    public string Number { get; set; } = "";
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; }
    public bool TaxEnabled { get; set; }
    public int TaxRatePpm { get; set; } = 100_000;
    public int ReducedRatePpm { get; set; }
    public string Currency { get; set; } = "AUD";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public int? RecurringScheduleId { get; set; }
    public int? ConvertedFromId { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<Transaction> Payments { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];
    public List<Reminder> Reminders { get; set; } = [];

    public long PaidCents => Payments.Where(p => p.Direction == Direction.In).Sum(p => p.InvoiceAmountCents);
}
