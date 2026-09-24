// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Desktop.Platform;

// any chromium browser can print a page to pdf the way webview2 does on windows
public static class BrowserLocator
{
    static readonly string[] MacApps =
    [
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
        "/Applications/Chromium.app/Contents/MacOS/Chromium",
        "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
    ];

    static readonly string[] LinuxCommands =
    [
        "google-chrome", "google-chrome-stable", "chromium", "chromium-browser",
        "microsoft-edge", "microsoft-edge-stable", "brave-browser", "brave",
    ];

    // INVOICEDESK_BROWSER lets someone point at a browser installed somewhere unusual
    public static string? Find() => Find(
        SystemDialog.CurrentOs, File.Exists, Environment.GetEnvironmentVariable("PATH"),
        Environment.GetEnvironmentVariable("INVOICEDESK_BROWSER"));

    public static string? Find(DialogOs os, Func<string, bool> exists, string? pathEnv, string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && exists(overridePath)) return overridePath;
        return os switch
        {
            DialogOs.Mac => MacApps.FirstOrDefault(exists),
            DialogOs.Windows => WindowsApps().FirstOrDefault(exists),
            _ => OnPath(pathEnv, exists),
        };
    }

    // snaps can only see their own folders, so printing has to happen inside one
    public static string? SnapName(string browserPath) =>
        browserPath.StartsWith("/snap/bin/", StringComparison.Ordinal) ? browserPath["/snap/bin/".Length..] : null;

    static string? OnPath(string? pathEnv, Func<string, bool> exists)
    {
        var dirs = (pathEnv ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries);
        foreach (var command in LinuxCommands)
            foreach (var dir in dirs)
            {
                var candidate = dir.TrimEnd('/') + "/" + command;
                if (exists(candidate)) return candidate;
            }
        return null;
    }

    static IEnumerable<string> WindowsApps()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
        {
            if (string.IsNullOrEmpty(root)) continue;
            yield return Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
            yield return Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
        }
    }
}
