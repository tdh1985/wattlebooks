// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Storage;
using InvoiceDesk.Ui;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App.Host;

public static class VirtualHosts
{
    // the pdf webview needs the same hosts or printed invoices lose their logo
    public static void Map(CoreWebView2 core, AppPaths paths)
    {
        core.SetVirtualHostNameToFolderMapping(new Uri(FilesUrl.FilesBase).Host, paths.DataRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping(new Uri(FilesUrl.LocalBase).Host, paths.LocalRoot, CoreWebView2HostResourceAccessKind.Allow);
    }
}
