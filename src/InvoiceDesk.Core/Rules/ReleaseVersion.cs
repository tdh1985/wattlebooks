// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

// github tags look like v1.2.0 and the build adds +commit to our own version
public static class ReleaseVersion
{
    public static string Display(string raw)
    {
        var s = raw.Trim().TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        return plus < 0 ? s : s[..plus];
    }

    public static bool IsNewer(string tag, string current) =>
        Version.TryParse(Display(tag), out var latest)
        && Version.TryParse(Display(current), out var mine)
        && latest > mine;
}
