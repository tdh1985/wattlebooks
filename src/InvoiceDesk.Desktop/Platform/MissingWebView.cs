// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Desktop.Platform;

// the linux webview is a separate package, so its absence gets a message rather than a crash
public static class MissingWebView
{
    public static bool IsCause(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is DllNotFoundException) return true;
        return false;
    }

    public static string Message(DialogOs os) => os switch
    {
        DialogOs.Linux => "InvoiceDesk needs WebKitGTK to show its window, and it isn't installed.\n\n" +
                          "On Ubuntu or Debian, install it with:\nsudo apt install libwebkit2gtk-4.1-0\n\nThen open InvoiceDesk again.",
        DialogOs.Mac => "InvoiceDesk couldn't open its window. It needs macOS 12 or later.",
        _ => "InvoiceDesk couldn't open its window because its web view is missing.",
    };
}
