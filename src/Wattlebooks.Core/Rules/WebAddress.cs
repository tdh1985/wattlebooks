// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Rules;

public static class WebAddress
{
    public static bool IsValid(string? url)
    {
        var value = url?.Trim() ?? "";
        return value.Length is > 0 and <= 200 && !value.Any(char.IsWhiteSpace) && Display(value).Contains('.');
    }

    // invoices read better without the scheme and trailing slash
    public static string Display(string url)
    {
        var value = url.Trim();
        foreach (var scheme in new[] { "https://", "http://" })
        {
            if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) value = value[scheme.Length..];
        }
        return value.TrimEnd('/');
    }

    public static string Href(string url)
    {
        var value = url.Trim();
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? value
            : "https://" + value;
    }
}
