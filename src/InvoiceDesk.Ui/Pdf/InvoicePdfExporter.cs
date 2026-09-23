// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using InvoiceDesk.Ui;
using InvoiceDesk.Ui.Components;
using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Ui.Pdf;

public sealed class InvoicePdfExporter(
    DocumentPdfExporter documents, InvoiceService invoices, ProfileService profiles, AppPaths paths, TimeProvider clock)
{
    public static string DefaultFileName(Invoice invoice) =>
        Format.SafeFileName($"{invoice.Number} - {invoice.Client?.Name}") + ".pdf";

    public async Task<string> ExportAsync(int invoiceId, string? targetPath = null)
    {
        var invoice = await invoices.GetAsync(invoiceId) ?? throw new ValidationException("This invoice no longer exists.");
        var profile = await profiles.GetAsync();
        var path = targetPath ?? Path.Combine(paths.Exports, DefaultFileName(invoice));
        await documents.ExportAsync<InvoiceDocument>(new Dictionary<string, object?>
        {
            [nameof(InvoiceDocument.Invoice)] = invoice,
            [nameof(InvoiceDocument.Profile)] = profile,
            [nameof(InvoiceDocument.LogoUrl)] = FilesUrl.Logo(profile),
            [nameof(InvoiceDocument.Today)] = clock.Today(),
        }, invoice.Number, ["css/invoice.css"], path);
        return path;
    }
}
