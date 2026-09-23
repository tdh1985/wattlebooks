// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.IO;
using System.Text.Json;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App.Pdf;

// prints through a hidden webview so pdfs match the on-screen preview exactly
public sealed class PdfPrinter(AppPaths paths, HostWindow host) : IDisposable
{
    const double CssPixelsPerInch = 96;

    // the remeasure is floored at the page height so this only shrinks a bit more
    const double ReflowSlackPx = 16;

    readonly SemaphoreSlim _gate = new(1, 1);
    CoreWebView2Environment? _env;
    CoreWebView2Controller? _controller;

    public async Task PrintAsync(string html, string pdfPath)
    {
        await _gate.WaitAsync();
        var page = Path.Combine(paths.Render, $"print-{Guid.NewGuid():N}.html");
        try
        {
            var core = await EnsureAsync();
            var paper = Format.Country.Paper;

            // pin the scale so the viewport floor is the page box on any display
            _controller!.RasterizationScale = 1.0;
            _controller.Bounds = new System.Drawing.Rectangle(0, 0,
                (int)Math.Ceiling(paper.WidthInches * CssPixelsPerInch),
                (int)Math.Ceiling(paper.HeightInches * CssPixelsPerInch));

            Directory.CreateDirectory(paths.Render);
            await File.WriteAllTextAsync(page, html);

            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) => loaded.TrySetResult(e.IsSuccess);
            core.NavigationCompleted += OnCompleted;
            try
            {
                core.Navigate(FilesUrl.ForLocal($"render/{Path.GetFileName(page)}"));
                if (!await loaded.Task.WaitAsync(TimeSpan.FromSeconds(20)))
                    throw new InvalidOperationException("The page didn't load for printing.");
            }
            finally
            {
                core.NavigationCompleted -= OnCompleted;
            }

            // scrollHeight is a whole px, so round up to avoid a false "just over" from that
            var pagePx = Math.Ceiling(paper.HeightInches * CssPixelsPerInch);
            var contentPx = await MeasureHeightAsync(core);
            var scale = PageFit.ScaleFor(contentPx, pagePx);
            if (scale < 1)
            {
                // a fresh document loads next time, so this can't leak into the next print
                await ZoomAsync(core, scale);
                var reflowedPx = await MeasureHeightAsync(core);
                var refit = PageFit.Refit(scale, reflowedPx, pagePx, ReflowSlackPx);
                if (refit != scale)
                {
                    scale = refit;
                    await ZoomAsync(core, scale);
                }
            }

            var settings = _env!.CreatePrintSettings();
            settings.Orientation = CoreWebView2PrintOrientation.Portrait;
            settings.PageWidth = paper.WidthInches;
            settings.PageHeight = paper.HeightInches;
            settings.MarginTop = 0;
            settings.MarginBottom = 0;
            settings.MarginLeft = 0;
            settings.MarginRight = 0;
            settings.ScaleFactor = 1;
            settings.ShouldPrintBackgrounds = true;
            settings.ShouldPrintHeaderAndFooter = false;

            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
            if (!await core.PrintToPdfAsync(pdfPath, settings))
                throw new IOException("Couldn't write the PDF. If it's open in another program, close it and try again.");
        }
        finally
        {
            try { File.Delete(page); }
            catch (IOException) { }
            _gate.Release();
        }
    }

    async Task<CoreWebView2> EnsureAsync()
    {
        if (_controller is not null) return _controller.CoreWebView2;
        if (host.Handle == IntPtr.Zero) throw new InvalidOperationException("The window isn't ready yet. Try again in a moment.");

        // its own profile folder so its options can never clash with the main webview
        _env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(paths.WebView, "print"));
        _controller = await _env.CreateCoreWebView2ControllerAsync(host.Handle);
        _controller.IsVisible = false;
        // otherwise a host monitor dpi change silently overrides the pinned scale
        _controller.ShouldDetectMonitorScaleChanges = false;
        // real bounds are set per print in PrintAsync, once the paper is known
        FilesUrl.MapHosts(_controller.CoreWebView2, paths);
        return _controller.CoreWebView2;
    }

    static async Task<double> MeasureHeightAsync(CoreWebView2 core)
    {
        var json = await core.ExecuteScriptAsync("document.documentElement.scrollHeight");
        // a failed script returns null so zero fits at scale 1 instead of throwing
        return JsonSerializer.Deserialize<double?>(json) ?? 0;
    }

    static Task ZoomAsync(CoreWebView2 core, double scale)
    {
        var value = scale.ToString(CultureInfo.InvariantCulture);
        return core.ExecuteScriptAsync($"document.documentElement.style.zoom = '{value}'");
    }

    public void Dispose()
    {
        _controller?.Close();
        _gate.Dispose();
    }
}
