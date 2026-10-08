// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.App.Host;

// the main window handle, needed by the hidden webview that prints pdfs
public sealed class HostWindow
{
    public IntPtr Handle { get; set; }
}
