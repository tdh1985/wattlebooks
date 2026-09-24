// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace InvoiceDesk.Desktop.Platform;

public enum DialogOs { Windows, Mac, Linux }

public enum DialogButtons { Ok, YesNo, YesNoCancel }

public enum DialogChoice { Ok, Yes, No, Cancel }

// startup questions come before photino has a window, so they use the os's own dialog tools
public static partial class SystemDialog
{
    public sealed record Command(string File, IReadOnlyList<string> Args);

    public static DialogOs CurrentOs =>
        OperatingSystem.IsWindows() ? DialogOs.Windows : OperatingSystem.IsMacOS() ? DialogOs.Mac : DialogOs.Linux;

    public static Command? CommandFor(DialogOs os, string title, string message, DialogButtons buttons, Func<string, bool> hasTool)
    {
        if (os == DialogOs.Mac) return Osascript(title, message, buttons);
        if (os != DialogOs.Linux) return null;
        if (hasTool("zenity")) return Zenity(title, message, buttons);
        if (hasTool("kdialog")) return Kdialog(title, message, buttons);
        return null;
    }

    public static DialogChoice Parse(string tool, DialogButtons buttons, int exitCode, string stdout)
    {
        if (buttons == DialogButtons.Ok) return DialogChoice.Ok;
        var closed = buttons == DialogButtons.YesNoCancel ? DialogChoice.Cancel : DialogChoice.No;
        return tool switch
        {
            // osascript exits 1 when its cancel button or escape is used
            "osascript" => exitCode != 0 ? closed : stdout.Contains("button returned:Yes") ? DialogChoice.Yes : DialogChoice.No,
            // zenity prints an extra button's label, closing it prints nothing, both exit 1
            "zenity" => exitCode == 0 ? DialogChoice.Yes : stdout.Trim() == "No" ? DialogChoice.No : closed,
            "kdialog" => exitCode switch { 0 => DialogChoice.Yes, 1 => DialogChoice.No, _ => closed },
            _ => closed,
        };
    }

    // null means nobody could be asked, so the caller picks the safe answer
    public static DialogChoice? Ask(string title, string message, DialogButtons buttons)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return AskWindows(title, message, buttons);
            var cmd = CommandFor(CurrentOs, title, message, buttons, OnPath);
            if (cmd is null)
            {
                Console.Error.WriteLine($"{title}: {message}");
                return null;
            }
            var start = new ProcessStartInfo(cmd.File) { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (var arg in cmd.Args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return Parse(cmd.File, buttons, process.ExitCode, stdout);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"{title}: {message}");
            return null;
        }
    }

    static bool OnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, tool)));

    static Command Osascript(string title, string message, DialogButtons buttons)
    {
        var list = buttons switch
        {
            DialogButtons.YesNoCancel => "buttons {\"Cancel\", \"No\", \"Yes\"} default button \"Yes\" cancel button \"Cancel\"",
            DialogButtons.YesNo => "buttons {\"No\", \"Yes\"} default button \"Yes\" cancel button \"No\"",
            _ => "buttons {\"OK\"} default button \"OK\"",
        };
        return new Command("osascript", ["-e", $"display dialog {AppleText(message)} with title {AppleText(title)} {list}"]);
    }

    static string AppleText(string text)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in text) sb.Append(c is '\\' or '"' ? "\\" + c : c.ToString());
        return sb.Append('"').ToString();
    }

    static Command Zenity(string title, string message, DialogButtons buttons) => buttons switch
    {
        DialogButtons.Ok => new Command("zenity", ["--info", "--no-markup", "--title", title, "--text", message]),
        DialogButtons.YesNo => new Command("zenity", ["--question", "--no-markup", "--title", title, "--text", message, "--ok-label", "Yes", "--cancel-label", "No"]),
        // no is the extra button, so escape or the close box lands on cancel like the other oses
        _ => new Command("zenity", ["--question", "--no-markup", "--title", title, "--text", message, "--ok-label", "Yes", "--cancel-label", "Cancel", "--extra-button", "No"]),
    };

    static Command Kdialog(string title, string message, DialogButtons buttons) =>
        new("kdialog", ["--title", title, buttons switch { DialogButtons.Ok => "--msgbox", DialogButtons.YesNo => "--yesno", _ => "--yesnocancel" }, message]);

    static DialogChoice AskWindows(string title, string message, DialogButtons buttons)
    {
        const uint YesNoCancel = 0x3, YesNo = 0x4, Warning = 0x30;
        var style = buttons switch { DialogButtons.YesNoCancel => YesNoCancel, DialogButtons.YesNo => YesNo, _ => 0u } | Warning;
        return MessageBoxW(IntPtr.Zero, message, title, style) switch
        {
            6 => DialogChoice.Yes,
            7 => DialogChoice.No,
            2 => DialogChoice.Cancel,
            _ => DialogChoice.Ok,
        };
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
