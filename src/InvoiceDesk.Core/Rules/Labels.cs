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

    public static string JoinAnd(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count switch
        {
            0 => "",
            1 => list[0],
            _ => $"{string.Join(", ", list[..^1])} and {list[^1]}",
        };
    }
}
