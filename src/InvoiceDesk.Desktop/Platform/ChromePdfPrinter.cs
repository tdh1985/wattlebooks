// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Platform;

namespace InvoiceDesk.Desktop.Platform;

// makes pdfs with a chromium browser running headless, filled in by the pdf task
public sealed class ChromePdfPrinter : IPdfPrinter
{
    public bool CanPrint => false;

    public Task PrintAsync(string html, string pdfPath) =>
        throw new PdfUnavailableException("PDFs aren't ready on this computer yet.");
}
