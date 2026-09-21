// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

// each name matches a doc-{name} class in invoice.css
public static class InvoiceTemplates
{
    public const string Classic = "classic";
    public const string Modern = "modern";
    public const string Minimal = "minimal";

    public static readonly string[] All = [Classic, Modern, Minimal];

    public static string Normalise(string? name)
    {
        var n = name?.Trim().ToLowerInvariant();
        return All.Contains(n) ? n! : Classic;
    }
}
