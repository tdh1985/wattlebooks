// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Ui;
using InvoiceDesk.Ui.Platform;

namespace InvoiceDesk.Desktop.Platform;

// makes pdfs with a chromium browser running headless, the engine webview2 prints with on windows
public sealed class ChromePdfPrinter(AppPaths paths, Func<string?> findBrowser, Action<string> openFile, TimeSpan limit) : IPdfPrinter
{
    const double CssPixelsPerInch = 96;

    // the remeasure is floored at the page height so this only shrinks a bit more
    const double ReflowSlackPx = 16;

    // kept between prints since a fresh profile costs every launch about 200 ms
    const string SharedProfile = "chrome-profile";

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly Lazy<string?> _browser = new(findBrowser);

    public ChromePdfPrinter(AppPaths paths, IDesktop desktop)
        : this(paths, BrowserLocator.Find, desktop.OpenFile, TimeSpan.FromSeconds(30)) { }

    public bool CanPrint => _browser.Value is not null;

    public async Task PrintAsync(string html, string pdfPath)
    {
        if (_browser.Value is not { } browser)
        {
            var page = Path.Combine(paths.Render, $"print-{Guid.NewGuid():N}.html");
            Directory.CreateDirectory(paths.Render);
            await File.WriteAllTextAsync(page, Prepare(html, null));
            openFile(page);
            throw new PdfUnavailableException(
                "No Chrome, Edge or Chromium was found to make the PDF, so it opened in your browser. Print it there and choose Save as PDF.");
        }

        await _gate.WaitAsync();
        var folder = WorkFolder(browser);
        var work = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            var paper = Format.Country.Paper;
            // scrollHeight is a whole px, so round up to avoid a false "just over" from that
            var fit = new PrintFit((int)Math.Round(paper.WidthInches * CssPixelsPerInch), Math.Ceiling(paper.HeightInches * CssPixelsPerInch), ReflowSlackPx);
            var pagePath = Path.Combine(work, "print.html");
            await File.WriteAllTextAsync(pagePath, Prepare(html, fit));
            var output = Path.Combine(work, "print.pdf");
            var page = new Uri(pagePath).AbsoluteUri;

            using (var claim = ClaimSharedProfile(folder))
            {
                // another copy of the app is printing, so this one takes a profile of its own
                var profile = claim is null ? Path.Combine(work, "profile") : Path.Combine(folder, SharedProfile);
                bool made;
                try { made = await TryPrintAsync(browser, profile, output, page); }
                catch (IOException) when (claim is not null)
                {
                    Clear(profile);
                    throw;
                }
                if (!made)
                {
                    // cleared so a broken profile can't fail every print after this one
                    if (claim is not null) Clear(profile);
                    if (!await TryPrintAsync(browser, Path.Combine(work, "retry-profile"), output, page))
                        throw new IOException("The browser didn't make the PDF. Try again, or update Chrome.");
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pdfPath))!);
            try { File.Move(output, pdfPath, overwrite: true); }
            catch (UnauthorizedAccessException) { throw new IOException("Couldn't write the PDF. If it's open in another program, close it and try again."); }
        }
        finally
        {
            Clear(work);
            _gate.Release();
        }
    }

    async Task<bool> TryPrintAsync(string browser, string profile, string output, string page)
    {
        await RunAsync(browser, ChromeArgs.Print(profile, output, page));
        return File.Exists(output);
    }

    // chrome locks its profile so only one copy of the app can use it at a time
    static FileStream? ClaimSharedProfile(string folder)
    {
        try { return new FileStream(Path.Combine(folder, SharedProfile + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    static void Clear(string folder)
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    string Prepare(string html, PrintFit? fit) =>
        PrintHtml.Prepare(html, FilesUrl.FilesBase, paths.DataRoot, FilesUrl.LocalBase, paths.LocalRoot, Format.Country.Paper.CssSize, fit);

    // a snap browser can only see its own folder, everything else prints from render
    string WorkFolder(string browser) => BrowserLocator.SnapName(browser) is { } snap
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "snap", snap, "common", "invoicedesk-print")
        : paths.Render;

    async Task RunAsync(string browser, string[] args)
    {
        var start = new ProcessStartInfo(browser)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("The browser couldn't be started to make the PDF.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            // a leftover child process can hold the output open after the browser itself exits
            await stdout.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            throw new IOException("The browser took too long to make the PDF. Close any stuck browser windows and try again.");
        }
    }
}
