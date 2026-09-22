// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.IO;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;

namespace InvoiceDesk.App.Ui;

public static class Format
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // set from the business profile at startup and whenever it changes
    public static CountryRules Country { get; private set; } = Australia.Rules;

    public static void UseCountry(string code) => Country = Countries.For(code);

    public static string Money(long cents) => Currencies.Format(cents, Country.Currency, Country.Currency);

    public static string Money(long cents, string currency) => Currencies.Format(cents, currency, Country.Currency);

    public static string Amount(long cents) => Currencies.Amount(cents);

    public static string MoneyList(IEnumerable<CurrencyAmount> amounts) =>
        Labels.JoinAnd(amounts.Select(a => Money(a.Cents, a.Currency)));

    // hmrc wants some vat return boxes in whole pounds
    public static string WholeMoney(long cents) =>
        (cents < 0 ? "-" : "") + Currencies.Find(Country.Currency)!.Symbol + (Math.Abs(cents) / 100).ToString("#,0", Invariant);

    public static string MoneyInput(long cents) => (cents / 100m).ToString("0.00", Invariant);

    public static long? ParseMoney(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // strips everything but digits, dot and minus so any currency mark parses
        var cleaned = new string(text.Where(c => char.IsAsciiDigit(c) || c is '.' or '-').ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Number, Invariant, out var value)
            ? MoneyMath.Round(value * 100)
            : null;
    }

    public static decimal? ParseQuantity(string? text) =>
        decimal.TryParse(text?.Replace(",", "").Trim(), NumberStyles.Number, Invariant, out var q) ? Math.Round(q, 2, MidpointRounding.AwayFromZero) : null;

    public static string Quantity(decimal q) => q.ToString("0.##", Invariant);

    public static string Date(DateOnly d) => d.ToString(Country.ShortDate, Invariant);

    public static string DateLong(DateOnly d) => d.ToString(Country.LongDate, Invariant);

    public static string DateShort(DateOnly d) => d.ToString(Country.DayMonth, Invariant);

    public static string MonthShort(DateOnly d) => d.ToString("MMM", Invariant);

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", Invariant);

    public static DateOnly? ParseIso(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static string Percent(int ppm) => (ppm / 10_000m).ToString("0.###", CultureInfo.InvariantCulture) + "%";

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
