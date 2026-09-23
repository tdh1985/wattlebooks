// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

public static class MoneyMath
{
    public const decimal PpmPerWhole = 1_000_000m;

    public static long Round(decimal cents) => (long)Math.Round(cents, 0, MidpointRounding.AwayFromZero);

    public static long LineAmount(decimal quantity, long unitPriceCents) => Round(quantity * unitPriceCents);

    public static long TaxOn(long taxableCents, int ratePpm) => Round(taxableCents * (decimal)ratePpm / PpmPerWhole);

    public static long TaxFromInclusive(long amountCents, int ratePpm) =>
        Round(amountCents * (decimal)ratePpm / (PpmPerWhole + ratePpm));

    // the inclusive total that carries this much tax, the inverse of the above
    public static long InclusiveFromTax(long taxCents, int ratePpm) =>
        ratePpm == 0 ? 0 : Round(taxCents * (PpmPerWhole + ratePpm) / ratePpm);

    // tax on a part payment, so return figures follow cash received
    public static long ProportionalTax(long paymentCents, long invoiceTaxCents, long invoiceTotalCents) =>
        invoiceTotalCents == 0 ? 0 : Round((decimal)paymentCents * invoiceTaxCents / invoiceTotalCents);
}
