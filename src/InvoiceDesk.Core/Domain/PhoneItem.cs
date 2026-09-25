// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public enum PhoneItemKind { Receipt = 0, DraftInvoice = 1 }

// every phone upload leaves a row so a retried pull never imports it twice
public class PhoneItem
{
    public int Id { get; set; }
    public string InboxId { get; set; } = "";
    public PhoneItemKind Kind { get; set; }

    // relative to the data root like attachments, empty for invoices
    public string StoredPath { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime TakenAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public int? InvoiceId { get; set; }
    public DateTime? DoneAt { get; set; }
    public string Problem { get; set; } = "";
}
