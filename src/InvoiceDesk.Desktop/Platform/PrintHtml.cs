// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceDesk.Desktop.Platform;

// turns the app's invoice html into a page a headless browser prints the same as webview2
public static partial class PrintHtml
{
    const string HeightAttribute = "data-invoicedesk-height";

    // webview2 is told these in its print settings, a browser has to read them from css
    const string ColourStyle = "html { print-color-adjust: exact; -webkit-print-color-adjust: exact; }";

    public static string Prepare(string html, string filesBase, string filesRoot, string localBase, string localRoot, string pageSize, double zoom, int? measureWidthPx)
    {
        var ready = Inline(Inline(html, filesBase, filesRoot), localBase, localRoot);
        var style = $"@page {{ size: {pageSize}; margin: 0; }} {ColourStyle}";
        if (zoom < 1) style += $" html {{ zoom: {zoom.ToString(CultureInfo.InvariantCulture)}; }}";
        if (measureWidthPx is { } width) style += $" html {{ width: {width}px; }}";
        ready = InsertBefore(ready, "</head>", $"<style>{style}</style>");
        if (measureWidthPx is not null)
        {
            // images and fonts have loaded by then, so this is the height that prints
            ready = InsertBefore(ready, "</body>",
                $"<script>addEventListener('load', () => document.documentElement.setAttribute('{HeightAttribute}', document.documentElement.scrollHeight));</script>");
        }
        return ready;
    }

    public static double ParseHeight(string dom)
    {
        var match = HeightPattern().Match(dom);
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    // a browser can't reach the app's own schemes, and a snap can't read hidden folders
    static string Inline(string html, string baseUrl, string root) =>
        Regex.Replace(html, "\"" + Regex.Escape(baseUrl) + "([^\"]*)\"", match =>
        {
            using var file = FileScheme.Serve(root, baseUrl + match.Groups[1].Value, out var type);
            if (file is null) return match.Value;
            using var bytes = new MemoryStream();
            file.CopyTo(bytes);
            return $"\"data:{type};base64,{Convert.ToBase64String(bytes.ToArray())}\"";
        });

    static string InsertBefore(string html, string tag, string insert)
    {
        var at = html.LastIndexOf(tag, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? html + insert : html.Insert(at, insert);
    }

    [GeneratedRegex("data-invoicedesk-height=\"([0-9.]+)\"")]
    private static partial Regex HeightPattern();
}
