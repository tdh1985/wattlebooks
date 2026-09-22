// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Net;
using InvoiceDesk.App.Host;
using InvoiceDesk.App.Ui;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.App.Pdf;

// any razor document into a pdf, the same way invoices are printed
public sealed class DocumentPdfExporter(IServiceProvider services, PdfPrinter printer)
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

        var css = string.Concat(cssFiles.Select(f => EmbeddedAssets.Instance.ReadText(f)));
        return $$"""
            <!DOCTYPE html>
            <html lang="{{Format.Country.HtmlLang}}">
            <head>
            <meta charset="utf-8">
            <title>{{WebUtility.HtmlEncode(title)}}</title>
            <style>html, body { margin: 0; background: #fff; }{{css}}</style>
            </head>
            <body>{{body}}</body>
            </html>
            """;
    }
}
