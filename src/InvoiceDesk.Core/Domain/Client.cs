// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public string Website { get; set; } = "";
    // empty means the same country as the business
    public string Country { get; set; } = "";
    public string Region { get; set; } = "";
    public string Currency { get; set; } = "";

    // printed on every invoice to this client, unlike Notes which stay private
    public string InvoiceNote { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    public List<Invoice> Invoices { get; set; } = [];

    public Client Clone() => new()
    {
        Id = Id, Name = Name, ContactName = ContactName, Email = Email, Phone = Phone,
        Address = Address, TaxNumber = TaxNumber, Website = Website, InvoiceNote = InvoiceNote, Notes = Notes,
        Country = Country, Region = Region, Currency = Currency, CreatedAt = CreatedAt, IsArchived = IsArchived,
    };
}
