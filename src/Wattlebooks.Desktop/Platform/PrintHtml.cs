// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;
using Wattlebooks.Core.Rules;

namespace Wattlebooks.Desktop.Platform;

// the paper in css px and the slack the second measure is floored by
public readonly record struct PrintFit(int WidthPx, double PagePx, double SlackPx);

// turns the app's invoice html into a page a headless browser prints the same as webview2
public static class PrintHtml
{
    // webview2 is told these in its print settings, a browser has to read them from css
    const string ColourStyle = "html { print-color-adjust: exact; -webkit-print-color-adjust: exact; }";

    // the js twin of PageFit.ScaleFor and Refit, pinned to it by a test
    public const string FitMaths = """
        function scaleFor(contentPx, pagePx, min) {
          if (contentPx <= 0 || pagePx <= 0 || contentPx <= pagePx) return 1;
          const scale = pagePx / contentPx;
          return scale >= min ? scale : 1;
        }
        function refit(firstScale, reflowedPx, pagePx, slackPx, min) {
          const extra = scaleFor(reflowedPx, pagePx - slackPx, min);
          return extra < 1 ? Math.max(firstScale * extra, min) : firstScale;
        }
        """;

    // runs on load since print-to-pdf waits for load before it prints
    public const string FitScript = """
        (() => {
        // numbers arrive as attributes so no page text is ever turned into code
        const fit = document.currentScript.dataset;
        const widthPx = Number(fit.widthPx), pagePx = Number(fit.pagePx), slackPx = Number(fit.slackPx), min = Number(fit.minScale);
        """ + "\n" + FitMaths + "\n" + """
        addEventListener('load', () => {
          const root = document.documentElement;
          // the paper's width so the text wraps the way it will on the page
          root.style.width = widthPx + 'px';
          let scale = scaleFor(root.scrollHeight, pagePx, min);
          if (scale < 1) {
            root.style.zoom = String(scale);
            scale = refit(scale, root.scrollHeight, pagePx, slackPx, min);
          }
          root.style.width = '';
          root.style.zoom = scale < 1 ? String(scale) : '';
          root.setAttribute('data-wattlebooks-scale', String(scale));
        });
        })();
        """;

    public static string Prepare(string html, string filesBase, string filesRoot, string localBase, string localRoot, string pageSize, PrintFit? fit)
    {
        var ready = Inline(Inline(html, filesBase, filesRoot), localBase, localRoot);
        ready = InsertBefore(ready, "</head>", $"<style>@page {{ size: {pageSize}; margin: 0; }} {ColourStyle}</style>");
        if (fit is { } box)
        {
            ready = InsertBefore(ready, "</body>",
                $"<script data-width-px=\"{Number(box.WidthPx)}\" data-page-px=\"{Number(box.PagePx)}\" data-slack-px=\"{Number(box.SlackPx)}\" data-min-scale=\"{Number(PageFit.MinScale)}\">{FitScript}</script>");
        }
        return ready;
    }

    static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

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
}
