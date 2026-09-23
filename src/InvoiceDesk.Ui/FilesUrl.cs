// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;

namespace InvoiceDesk.Ui;

// made-up addresses mapped onto folders let the webview show receipts and logos
public static class FilesUrl
{
    // each host maps these its own way, windows uses webview2 virtual hosts
    public static string FilesBase { get; private set; } = "https://files.invoicedesk.example/";

    // staging and render files stay on this pc, apart from the synced data
    public static string LocalBase { get; private set; } = "https://local.invoicedesk.example/";

    public static void Configure(string filesBase, string localBase)
    {
        FilesBase = filesBase.EndsWith('/') ? filesBase : filesBase + "/";
        LocalBase = localBase.EndsWith('/') ? localBase : localBase + "/";
    }

    public static string For(string relativePath) => FilesBase + relativePath.TrimStart('/');

    public static string ForLocal(string relativePath) => LocalBase + relativePath.TrimStart('/');

    public static string? Logo(BusinessProfile? profile) =>
        profile?.LogoAttachment is { } logo ? For(logo.StoredPath) : null;
}
