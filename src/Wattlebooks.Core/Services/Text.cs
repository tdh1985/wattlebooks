// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Services;

internal static class Text
{
    public static string Clean(string? value) => value?.Trim() ?? "";

    public static bool Has(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
