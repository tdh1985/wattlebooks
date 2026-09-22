// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

public static class Countries
{
    public static IReadOnlyList<CountryRules> All { get; } = [Australia.Rules];

    // anything unknown is treated as australia, which is what 1.1.0 data is
    public static CountryRules For(string? code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) ?? Australia.Rules;

    public static bool IsSupported(string? code) =>
        All.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    public static string FromRegion(string? twoLetterRegion) =>
        IsSupported(twoLetterRegion) ? For(twoLetterRegion).Code : Australia.Rules.Code;
}
