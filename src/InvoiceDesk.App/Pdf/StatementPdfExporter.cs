// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using InvoiceDesk.App.Ui;
using InvoiceDesk.App.Ui.Components;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.App.Pdf;

public sealed class StatementPdfExporter(DocumentPdfExporter documents, StatementService statements, ProfileService profiles, AppPaths paths)
{
    public async Task<(string Path, Statement Statement)> ExportAsync(int clientId)
    {
        var statement = await statements.BuildAsync(clientId);
        var profile = await profiles.GetAsync();
        var name = Format.SafeFileName($"Statement - {statement.Client.Name} - {Csv.Date(statement.AsOf)}") + ".pdf";
        var path = Path.Combine(paths.Exports, name);
        await documents.ExportAsync<StatementDocument>(new Dictionary<string, object?>
        {
            [nameof(StatementDocument.Statement)] = statement,
            [nameof(StatementDocument.Profile)] = profile,
            [nameof(StatementDocument.LogoUrl)] = FilesUrl.Logo(profile),
        }, $"Statement for {statement.Client.Name}", ["css/invoice.css"], path);
        return (path, statement);
    }
}
