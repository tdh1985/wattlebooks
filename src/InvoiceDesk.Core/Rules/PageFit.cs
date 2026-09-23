// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Rules;

// documents only just over a page look better shrunk than split
public static class PageFit
{
    public const double MinScale = 0.82;

    public static double ScaleFor(double contentPx, double pagePx)
    {
        if (contentPx <= 0 || pagePx <= 0 || contentPx <= pagePx) return 1;
        var scale = pagePx / contentPx;
        return scale >= MinScale ? scale : 1;
    }

    // the reflow after zooming can still overflow, so shrink once more if needed
    public static double Refit(double firstScale, double reflowedPx, double pagePx, double slackPx)
    {
        var extra = ScaleFor(reflowedPx, pagePx - slackPx);
        return extra < 1 ? Math.Max(firstScale * extra, MinScale) : firstScale;
    }
}
