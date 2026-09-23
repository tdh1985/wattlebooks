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
}
