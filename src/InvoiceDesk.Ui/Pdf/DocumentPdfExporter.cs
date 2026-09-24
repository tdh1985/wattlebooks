// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Net;
using System.Text.RegularExpressions;
using InvoiceDesk.Ui.Host;
using InvoiceDesk.Ui;
using InvoiceDesk.Ui.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Ui.Pdf;

// any razor document into a pdf, the same way invoices are printed
public sealed class DocumentPdfExporter(IServiceProvider services, IPdfPrinter printer)
{
    public async Task ExportAsync<TDocument>(IDictionary<string, object?> parameters, string title, IEnumerable<string> cssFiles, string path)
        where TDocument : IComponent
    {
        var html = await RenderAsync<TDocument>(parameters, title, cssFiles);
        await printer.PrintAsync(html, path);
    }

    async Task<string> RenderAsync<TDocument>(IDictionary<string, object?> parameters, string title, IEnumerable<string> cssFiles)
        where TDocument : IComponent
    {
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var body = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TDocument>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });

        var css = BundledFonts.Value + string.Concat(cssFiles.Select(f => EmbeddedAssets.Instance.ReadText(f)));
        // @page can't read a css variable, so the country's size is inlined here
        var paperSize = Format.Country.Paper.CssSize;
        // keep the margin reset, a4 only fits its viewport with body margins gone
        return $$"""
            <!DOCTYPE html>
            <html lang="{{Format.Country.HtmlLang}}">
            <head>
            <meta charset="utf-8">
            <title>{{WebUtility.HtmlEncode(title)}}</title>
            <style>html, body { margin: 0; background: #fff; }{{css}}@page { size: {{paperSize}}; }@page long-doc { size: {{paperSize}}; }</style>
            </head>
            <body>{{body}}</body>
            </html>
            """;
    }

    // the pdf page loads from a file, so its font files travel inside the css
    static readonly Lazy<string> BundledFonts = new(() =>
        Regex.Replace(EmbeddedAssets.Instance.ReadText("css/fonts.css"), @"url\(""\.\./(fonts/[^""]+)""\)", m =>
        {
            using var file = EmbeddedAssets.Instance.GetFileInfo(m.Groups[1].Value).CreateReadStream();
            using var bytes = new MemoryStream();
            file.CopyTo(bytes);
            return $"url(data:font/woff2;base64,{Convert.ToBase64String(bytes.ToArray())})";
        }));
}
