// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;

namespace InvoiceDesk.Core.Rules;

// symbol is for the home currency, prefix is for anyone else's
public sealed record CurrencyInfo(string Code, string Name, string Symbol, string Prefix);

public static class Currencies
{
    public static IReadOnlyList<CurrencyInfo> All { get; } =
    [
        new("AUD", "Australian dollar", "$", "A$"),
        new("NZD", "New Zealand dollar", "$", "NZ$"),
        new("GBP", "British pound", "£", "£"),
        new("CAD", "Canadian dollar", "$", "C$"),
        new("USD", "US dollar", "$", "US$"),
        new("EUR", "Euro", "€", "€"),
        new("SGD", "Singapore dollar", "$", "S$"),
        new("HKD", "Hong Kong dollar", "$", "HK$"),
        new("CHF", "Swiss franc", "CHF ", "CHF "),
        new("SEK", "Swedish krona", "SEK ", "SEK "),
        new("NOK", "Norwegian krone", "NOK ", "NOK "),
        new("DKK", "Danish krone", "DKK ", "DKK "),
        new("ZAR", "South African rand", "R", "R"),
    ];

    public static CurrencyInfo? Find(string? code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    public static bool IsSupported(string? code) => Find(code) is not null;

    public static string Label(string code) => Find(code) is { } c ? $"{c.Code} – {c.Name}" : code;

    // what goes in front of an amount, so a money field can show it too
    public static string Mark(string currency, string homeCurrency) =>
        Find(currency) is not { } info
            ? currency.ToUpperInvariant() + " "
            : string.Equals(currency, homeCurrency, StringComparison.OrdinalIgnoreCase) ? info.Symbol : info.Prefix;

    public static string Format(long cents, string currency, string homeCurrency) =>
        (cents < 0 ? "-" : "") + Mark(currency, homeCurrency) + Amount(Math.Abs(cents));

    public static string Amount(long cents) => (cents / 100m).ToString("#,0.00", CultureInfo.InvariantCulture);
}
