// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public long UnitPriceCents { get; set; }
    public TaxCode TaxCode { get; set; }

    public InvoiceLine Copy() => new()
    {
        SortOrder = SortOrder, Description = Description, Quantity = Quantity,
        UnitPriceCents = UnitPriceCents, TaxCode = TaxCode,
    };
}
