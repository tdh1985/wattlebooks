// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using InvoiceDesk.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App.Pdf;

// prints through a hidden webview so pdfs match the on-screen preview exactly
public sealed class PdfPrinter(AppPaths paths, HostWindow host) : IDisposable
{
    const double A4WidthInches = 8.27;
    const double A4HeightInches = 11.69;

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

            var settings = _env!.CreatePrintSettings();
            settings.Orientation = CoreWebView2PrintOrientation.Portrait;
            settings.PageWidth = A4WidthInches;
            settings.PageHeight = A4HeightInches;
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
        _controller.Bounds = new System.Drawing.Rectangle(0, 0, 794, 1123);
        FilesUrl.MapHosts(_controller.CoreWebView2, paths);
        return _controller.CoreWebView2;
    }

    public void Dispose()
    {
        _controller?.Close();
        _gate.Dispose();
    }
}
