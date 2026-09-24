// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using InvoiceDesk.Core.Rules;
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
            await File.WriteAllTextAsync(page, Prepare(html, 1, null));
            openFile(page);
            throw new PdfUnavailableException(
                "No Chrome, Edge or Chromium was found to make the PDF, so it opened in your browser. Print it there and choose Save as PDF.");
        }

        await _gate.WaitAsync();
        var work = Path.Combine(WorkFolder(browser), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            var paper = Format.Country.Paper;
            var widthPx = (int)Math.Round(paper.WidthInches * CssPixelsPerInch);
            // scrollHeight is a whole px, so round up to avoid a false "just over" from that
            var pagePx = Math.Ceiling(paper.HeightInches * CssPixelsPerInch);

            var scale = PageFit.ScaleFor(await MeasureAsync(browser, work, html, 1, widthPx), pagePx);
            if (scale < 1)
            {
                var reflowedPx = await MeasureAsync(browser, work, html, scale, widthPx);
                scale = PageFit.Refit(scale, reflowedPx, pagePx, ReflowSlackPx);
            }

            var pagePath = Path.Combine(work, "print.html");
            await File.WriteAllTextAsync(pagePath, Prepare(html, scale, null));
            var output = Path.Combine(work, "print.pdf");
            await RunAsync(browser, ChromeArgs.Print(Path.Combine(work, "profile"), output, new Uri(pagePath).AbsoluteUri));
            if (!File.Exists(output)) throw new IOException("The browser didn't make the PDF. Try again, or update Chrome.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pdfPath))!);
            try { File.Move(output, pdfPath, overwrite: true); }
            catch (UnauthorizedAccessException) { throw new IOException("Couldn't write the PDF. If it's open in another program, close it and try again."); }
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            _gate.Release();
        }
    }

    async Task<double> MeasureAsync(string browser, string work, string html, double zoom, int widthPx)
    {
        var page = Path.Combine(work, "measure.html");
        await File.WriteAllTextAsync(page, Prepare(html, zoom, widthPx));
        var dom = await RunAsync(browser, ChromeArgs.Measure(Path.Combine(work, "profile"), new Uri(page).AbsoluteUri));
        // a failed measure reads as zero, which prints at full size rather than failing
        return PrintHtml.ParseHeight(dom);
    }

    string Prepare(string html, double zoom, int? measureWidthPx) =>
        PrintHtml.Prepare(html, FilesUrl.FilesBase, paths.DataRoot, FilesUrl.LocalBase, paths.LocalRoot, Format.Country.Paper.CssSize, zoom, measureWidthPx);

    // a snap browser can only see its own folder, everything else prints from render
    string WorkFolder(string browser) => BrowserLocator.SnapName(browser) is { } snap
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "snap", snap, "common", "invoicedesk-print")
        : paths.Render;

    async Task<string> RunAsync(string browser, string[] args)
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
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            throw new IOException("The browser took too long to make the PDF. Close any stuck browser windows and try again.");
        }
        return await stdout;
    }
}
