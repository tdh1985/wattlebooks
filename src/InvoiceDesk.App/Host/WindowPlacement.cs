// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Windows;

using InvoiceDesk.Ui.Host;

namespace InvoiceDesk.App.Host;

public static class WindowPlacement
{
    public static void Restore(Window window, AppPrefs prefs)
    {
        if (prefs is { Left: { } left, Top: { } top, Width: { } width, Height: { } height } && FitsOnScreen(left, top, width, height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
            window.Width = width;
            window.Height = height;
        }
        if (prefs.Maximized) window.WindowState = WindowState.Maximized;
    }

    public static void Save(Window window, AppPrefs prefs)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        prefs.Left = bounds.Left;
        prefs.Top = bounds.Top;
        prefs.Width = bounds.Width;
        prefs.Height = bounds.Height;
        prefs.Maximized = window.WindowState == WindowState.Maximized;
    }

    // a monitor may have been unplugged since last time
    static bool FitsOnScreen(double left, double top, double width, double height) =>
        width >= 400 && height >= 300
        && left >= SystemParameters.VirtualScreenLeft - 8
        && top >= SystemParameters.VirtualScreenTop - 8
        && left + 100 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top + 50 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
}
