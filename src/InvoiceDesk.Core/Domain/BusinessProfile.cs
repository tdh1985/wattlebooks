// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public class BusinessProfile
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string Name { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Website { get; set; } = "";
    public int? LogoAttachmentId { get; set; }
    public Attachment? LogoAttachment { get; set; }
    public string BankAccountName { get; set; } = "";
    public string BankCode { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccentColour { get; set; } = "#2E6B57";
    public string InvoiceTemplate { get; set; } = "classic";
    public int PaymentTermsDays { get; set; } = 14;
    public string InvoicePrefix { get; set; } = "INV-";
    public int NextInvoiceNumber { get; set; } = 1;
    public int NumberPadding { get; set; } = 4;
    public string QuotePrefix { get; set; } = "QUO-";
    public int NextQuoteNumber { get; set; } = 1;
    public int QuoteValidDays { get; set; } = 30;
    public bool TaxRegistered { get; set; } = true;
    public int TaxRatePpm { get; set; } = 100_000;
    public string Country { get; set; } = "AU";
    // canadian province, which sets the gst or hst rate
    public string Region { get; set; } = "";
    public int ReducedRatePpm { get; set; }
    public int TaxPeriodMonths { get; set; } = 3;
    public int TaxPeriodEndMonth { get; set; } = 3;
    public string SwiftCode { get; set; } = "";
    public string OverseasNote { get; set; } = "";
    public string FooterNote { get; set; } = "Thank you for your business.";
    public string DefaultInvoiceNotes { get; set; } = "";

    // for the owner only, never printed
    public string PrivateNotes { get; set; } = "";

    // ties a phone login to this database so two businesses never mix
    public string SyncId { get; set; } = "";
}
