// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public class Transaction
{
    public int Id { get; set; }
    public Direction Direction { get; set; }
    public DateOnly Date { get; set; }
    public long AmountCents { get; set; }
    public long TaxCents { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Party { get; set; } = "";
    public string Description { get; set; } = "";
    public string Notes { get; set; } = "";
    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Attachment> Attachments { get; set; } = [];

    // what the client paid in the invoice's own currency, when that isn't ours
    public long? ForeignAmountCents { get; set; }
    public string ForeignCurrency { get; set; } = "";

    public long InvoiceAmountCents => ForeignAmountCents ?? AmountCents;

    public long ExTaxCents => AmountCents - TaxCents;
}
