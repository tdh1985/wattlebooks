// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Rules;

public static class Abn
{
    static readonly int[] Weights = [10, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19];

    // the ato checksum catches typos before an abn lands on an invoice
    public static bool IsValid(string? abn)
    {
        var digits = Digits(abn);
        if (digits is null) return false;
        var sum = 0;
        for (var i = 0; i < 11; i++) sum += (digits[i] - (i == 0 ? 1 : 0)) * Weights[i];
        return sum % 89 == 0;
    }

    public static string Format(string abn)
    {
        var digits = Digits(abn);
        if (digits is null) return abn;
        var s = string.Concat(digits);
        return $"{s[..2]} {s[2..5]} {s[5..8]} {s[8..]}";
    }

    static int[]? Digits(string? abn)
    {
        if (string.IsNullOrWhiteSpace(abn)) return null;
        var compact = abn.Replace(" ", "");
        if (compact.Length != 11 || !compact.All(char.IsAsciiDigit)) return null;
        return compact.Select(c => c - '0').ToArray();
    }
}
