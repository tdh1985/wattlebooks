// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Platform;
using Microsoft.Win32;

namespace InvoiceDesk.App.Host;

public sealed class WindowsPlatform : IPlatformInfo
{
    public string SystemName => "Windows";

    public bool CanBeTranslucent => WindowChrome.SupportsMica;

    public bool SystemPrefersDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }
}
