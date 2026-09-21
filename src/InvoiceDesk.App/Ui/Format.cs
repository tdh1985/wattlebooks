// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.IO;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.App.Ui;

public static class Format
{
    static readonly CultureInfo Au = CultureInfo.GetCultureInfo("en-AU");
    static readonly CultureInfo Months = CultureInfo.InvariantCulture;

    public static string Money(long cents) => (cents / 100m).ToString("C2", Au);

    public static string Amount(long cents) => (cents / 100m).ToString("N2", Au);

    public static string MoneyInput(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    public static long? ParseMoney(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleaned = text.Replace("$", "").Replace(",", "").Replace(" ", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? MoneyMath.Round(value * 100)
            : null;
    }

    public static decimal? ParseQuantity(string? text) =>
        decimal.TryParse(text?.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var q) ? Math.Round(q, 2, MidpointRounding.AwayFromZero) : null;

    public static string Quantity(decimal q) => q.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", Au);

    public static string DateLong(DateOnly d) => d.ToString("d MMMM yyyy", Months);

    public static string DateShort(DateOnly d) => d.ToString("d MMM", Months);

    public static string MonthShort(DateOnly d) => d.ToString("MMM", Months);

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? ParseIso(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static string Percent(int basisPoints) => (basisPoints / 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };

    public static string Due(DateOnly due, DateOnly today)
    {
        var days = due.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "Due today",
            1 => "Due tomorrow",
            > 1 => $"Due in {days} days",
            -1 => "1 day overdue",
            _ => $"{-days} days overdue",
        };
    }

    public static string ValidUntil(DateOnly until, DateOnly today)
    {
        var days = until.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "Last day to accept",
            1 => "Valid for 1 more day",
            > 1 => $"Valid for {days} more days",
            -1 => "Expired yesterday",
            _ => $"Expired {-days} days ago",
        };
    }

    public static string Status(DisplayStatus s) => Labels.Status(s);

    public static string Method(PaymentMethod m) => Labels.Method(m);

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "ID",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}",
        };
    }

    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "invoice" : cleaned;
    }
}
