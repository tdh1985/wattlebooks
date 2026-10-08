// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Ui.Platform;

public interface IPdfPrinter
{
    // false when this pc has nothing that can turn html into a pdf
    bool CanPrint { get; }

    Task PrintAsync(string html, string pdfPath);
}
