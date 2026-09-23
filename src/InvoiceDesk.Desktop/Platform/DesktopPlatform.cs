// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Platform;

namespace InvoiceDesk.Desktop.Platform;

public sealed class DesktopPlatform : IPlatformInfo
{
    public string SystemName => SystemDialog.CurrentOs switch
    {
        DialogOs.Mac => "macOS",
        DialogOs.Windows => "Windows",
        _ => "Linux",
    };

    public bool CanBeTranslucent => false;

    // the page paints from prefers-color-scheme and reports back, which settles it
    public bool SystemPrefersDark() => false;
}
