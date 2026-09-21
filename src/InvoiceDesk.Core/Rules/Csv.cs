// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;

namespace InvoiceDesk.Core.Rules;

// files for an accountant, so they have to open cleanly in excel
public static class Csv
{
    static readonly char[] NeedsQuotes = [',', '"', '\r', '\n'];

    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // excel runs cells starting with these as formulas, even from a csv
        if (value[0] is '=' or '+' or '-' or '@') value = "'" + value;
        return value.IndexOfAny(NeedsQuotes) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public static string Money(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Line(params string[] cells) => string.Join(',', cells);
}
