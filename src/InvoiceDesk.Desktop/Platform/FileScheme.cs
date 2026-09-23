// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Desktop.Platform;

// serves receipts and logos to the webview from one folder, the way webview2 virtual hosts do on windows
public static class FileScheme
{
    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".pdf"] = "application/pdf",
        [".html"] = "text/html",
        [".css"] = "text/css",
        [".txt"] = "text/plain",
    };

    // null means not found, including anything that tries to leave the root
    public static Stream? Serve(string root, string url, out string contentType)
    {
        contentType = "application/octet-stream";
        var scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) return null;
        var rest = url[(scheme + 3)..];
        var slash = rest.IndexOf('/');
        if (slash < 0) return null;
        var relative = rest[(slash + 1)..];
        var cut = relative.IndexOfAny(['?', '#']);
        if (cut >= 0) relative = relative[..cut];
        relative = Uri.UnescapeDataString(relative).Replace('\\', '/');
        if (relative.Length == 0 || Path.IsPathRooted(relative)) return null;

        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var fence = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(fence, comparison) || !File.Exists(full)) return null;

        contentType = Types.GetValueOrDefault(Path.GetExtension(full), "application/octet-stream");
        return File.OpenRead(full);
    }
}
