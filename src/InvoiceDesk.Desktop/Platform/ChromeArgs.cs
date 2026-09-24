// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Desktop.Platform;

public static class ChromeArgs
{
    // a throwaway profile keeps the user's own browser windows and settings out of it
    static string[] Common(string profileDir) =>
        ["--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--hide-scrollbars", $"--user-data-dir={profileDir}"];

    public static string[] Measure(string profileDir, string pageUri) =>
        [.. Common(profileDir), "--virtual-time-budget=5000", "--dump-dom", pageUri];

    public static string[] Print(string profileDir, string pdfPath, string pageUri) =>
        [.. Common(profileDir), "--no-pdf-header-footer", $"--print-to-pdf={pdfPath}", pageUri];
}
