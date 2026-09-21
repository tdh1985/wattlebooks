// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Windows.Media;

namespace InvoiceDesk.App.Host;

// tints the windows 11 title bar to match the desk so the chrome disappears
public static class WindowChrome
{
    const int UseImmersiveDarkMode = 20;
    const int CaptionColour = 35;
    const int TextColour = 36;
    const int SystemBackdropType = 38;
    const int BackdropNone = 1;
    const int BackdropMica = 2;
    const int ColourDefault = unchecked((int)0xFFFFFFFF);

    public static bool SupportsMica { get; } = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    public static void Apply(IntPtr hwnd, bool dark, Color caption, Color text, bool mica)
    {
        var darkFlag = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref darkFlag, sizeof(int));

        // a painted caption would sit on top of the mica, so hand it back to windows
        var captionRef = mica ? ColourDefault : ToColorRef(caption);
        var textRef = mica ? ColourDefault : ToColorRef(text);
        _ = DwmSetWindowAttribute(hwnd, CaptionColour, ref captionRef, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, TextColour, ref textRef, sizeof(int));

        if (!SupportsMica) return;
        var margins = mica ? new Margins(-1) : new Margins(0);
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
        var backdrop = mica ? BackdropMica : BackdropNone;
        _ = DwmSetWindowAttribute(hwnd, SystemBackdropType, ref backdrop, sizeof(int));
    }

    static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    [StructLayout(LayoutKind.Sequential)]
    readonly struct Margins(int all)
    {
        readonly int _left = all;
        readonly int _right = all;
        readonly int _top = all;
        readonly int _bottom = all;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
