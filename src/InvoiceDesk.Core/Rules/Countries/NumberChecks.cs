// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceDesk.Core.Rules;

// checksums catch a mistyped digit before a number lands on an invoice
public static partial class NumberChecks
{
    public static string Digits(string? s) => new((s ?? "").Where(char.IsAsciiDigit).ToArray());

    static bool DigitsAndSeparators(string? s) => (s ?? "").All(c => char.IsAsciiDigit(c) || c is ' ' or '-');

    // digits grouped as printed, or text as typed if it doesn't fit
    public static string Group(string value, params int[] sizes)
    {
        var d = Digits(value);
        if (!DigitsAndSeparators(value) || d.Length != sizes.Sum()) return value;
        var parts = new List<string>();
        var at = 0;
        foreach (var size in sizes)
        {
            parts.Add(d.Substring(at, size));
            at += size;
        }
        return string.Join("-", parts);
    }

    // the ird falls back to a second set of weights when the first gives 10
    public static bool Ird(string? value)
    {
        if (!DigitsAndSeparators(value)) return false;
        var d = Digits(value);
        if (d.Length is < 8 or > 9) return false;
        var n = long.Parse(d, CultureInfo.InvariantCulture);
        if (n <= 10_000_000 || n >= 150_000_000) return false;
        var body = d[..^1].PadLeft(8, '0');
        var check = IrdCheck(body, [3, 2, 7, 6, 5, 4, 3, 2]);
        if (check == 10) check = IrdCheck(body, [7, 4, 3, 2, 5, 2, 7, 6]);
        return check == d[^1] - '0';
    }

    static int IrdCheck(string body, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < 8; i++) sum += (body[i] - '0') * weights[i];
        var r = sum % 11;
        return r == 0 ? 0 : 11 - r;
    }

    public static string IrdDisplay(string value)
    {
        var d = Digits(value);
        return d.Length switch
        {
            8 => $"{d[..2]}-{d[2..5]}-{d[5..]}",
            9 => $"{d[..3]}-{d[3..6]}-{d[6..]}",
            _ => value,
        };
    }

    public static string UkVatCompact(string? value)
    {
        var s = (value ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();
        return s.StartsWith("GB", StringComparison.Ordinal) ? s[2..] : s;
    }

    // older numbers pass mod 97, and those issued since 2010 pass after adding 55
    public static bool UkVat(string? value)
    {
        var s = UkVatCompact(value);
        if (s.Length is not (9 or 12) || !s.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 7; i++) sum += (s[i] - '0') * (8 - i);
        var total = sum + int.Parse(s[7..9], CultureInfo.InvariantCulture);
        return total % 97 == 0 || (total + 55) % 97 == 0;
    }

    public static string UkVatDisplay(string value)
    {
        var s = UkVatCompact(value);
        if (s.Length is not (9 or 12) || !s.All(char.IsAsciiDigit)) return value;
        var main = $"GB {s[..3]} {s[3..7]} {s[7..9]}";
        return s.Length == 12 ? $"{main} {s[9..]}" : main;
    }

    public static string CanadaGstCompact(string? value) =>
        (value ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();

    // business number is luhn checked, gst/hst account ends in rt plus four digits
    public static bool CanadaGst(string? value)
    {
        var s = CanadaGstCompact(value);
        return s.Length == 15 && s[..9].All(char.IsAsciiDigit) && s[9..11] == "RT"
            && s[11..].All(char.IsAsciiDigit) && Luhn(s[..9]);
    }

    public static string CanadaGstDisplay(string value)
    {
        var s = CanadaGstCompact(value);
        return s.Length == 15 ? $"{s[..9]} {s[9..]}" : value;
    }

    public static bool Luhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }

    public static bool Ein(string? value) => DigitsAndSeparators(value) && Digits(value).Length == 9;

    public static string EinDisplay(string value)
    {
        var d = Digits(value);
        return d.Length == 9 ? $"{d[..2]}-{d[2..]}" : value;
    }

    public static bool AbaRouting(string? value)
    {
        if (!DigitsAndSeparators(value)) return false;
        var d = Digits(value);
        if (d.Length != 9) return false;
        int D(int i) => d[i] - '0';
        return (3 * (D(0) + D(3) + D(6)) + 7 * (D(1) + D(4) + D(7)) + D(2) + D(5) + D(8)) % 10 == 0;
    }

    public static bool Swift(string? value) => Bic().IsMatch((value ?? "").Replace(" ", "").ToUpperInvariant());

    // keep typed form unless only digits and separators
    public static string DigitsOnly(string value) => DigitsAndSeparators(value) ? Digits(value) : value;

    public static bool DigitCount(string value, int min, int max) =>
        value.All(char.IsAsciiDigit) && value.Length >= min && value.Length <= max;

    [GeneratedRegex("^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex Bic();
}
