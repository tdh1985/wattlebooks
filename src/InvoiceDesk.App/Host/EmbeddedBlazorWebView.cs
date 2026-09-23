// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Host;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.FileProviders;

namespace InvoiceDesk.App.Host;

// serves wwwroot from resources inside the exe instead of loose files on disk
public sealed class EmbeddedBlazorWebView : BlazorWebView
{
    public override IFileProvider CreateFileProvider(string contentRootDir) => EmbeddedAssets.Instance;
}
