// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Core.Rules;

// shared by the screens and the csv files so both use the same words
public static class Labels
{
    public static string Status(DisplayStatus s) => s switch
    {
        DisplayStatus.PartPaid => "Part-paid",
        _ => s.ToString(),
    };

    public static string Method(PaymentMethod m) => m switch
    {
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.None => "",
        _ => m.ToString(),
    };

    public static string Direction(Direction d) => d == Domain.Direction.In ? "Money in" : "Money out";
}
