// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;

namespace InvoiceDesk.Core.Rules;

public sealed record WorldCountry(string Code, string Name);

// every country a client can be in, named by windows so none are typed
public static class WorldCountries
{
    static readonly Lazy<IReadOnlyList<WorldCountry>> List = new(Build);

    public static IReadOnlyList<WorldCountry> All => List.Value;

    public static bool IsKnown(string? code) =>
        code is { Length: 2 } && All.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    public static string Name(string code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code;

    public static string? CurrencyOf(string code)
    {
        if (!IsKnown(code)) return null;
        try { return new RegionInfo(code.ToUpperInvariant()).ISOCurrencySymbol; }
        catch (ArgumentException) { return null; }
    }

    static IReadOnlyList<WorldCountry> Build() =>
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(Region)
            .OfType<RegionInfo>()
            .Where(r => r.TwoLetterISORegionName.Length == 2 && r.TwoLetterISORegionName.All(char.IsAsciiLetterUpper))
            .GroupBy(r => r.TwoLetterISORegionName)
            .Select(g => new WorldCountry(g.Key, g.First().EnglishName))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    static RegionInfo? Region(CultureInfo culture)
    {
        try { return new RegionInfo(culture.Name); }
        catch (ArgumentException) { return null; }
    }
}
