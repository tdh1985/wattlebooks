// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using InvoiceDesk.Ui.Platform;

namespace InvoiceDesk.Desktop.Platform;

// shell and dialog calls for macos and linux, and windows when photino runs there for testing
public sealed class UnixDesktop(PhotinoWindowRef window, DialogOs os, Action<SystemDialog.Command> run) : IDesktop
{
    public UnixDesktop(PhotinoWindowRef window) : this(window, SystemDialog.CurrentOs, Start) { }

    public void OpenFile(string path) => Open(path);

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Open(path);
    }

    public void ShowInFolder(string path)
    {
        switch (os)
        {
            case DialogOs.Mac: run(new("open", ["-R", path])); break;
            case DialogOs.Windows: run(new("explorer.exe", [$"/select,{path}"])); break;
            // file managers differ on selecting a file, so linux just opens its folder
            default: run(new("xdg-open", [path.LastIndexOf('/') is > 0 and var cut ? path[..cut] : "/"])); break;
        }
    }

    // web links only, so a bad string can't launch a program
    public void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            Open(uri.AbsoluteUri);
    }

    public void OpenMail(string mailtoUrl)
    {
        if (mailtoUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) Open(mailtoUrl);
    }

    public string? SaveFileAs(string defaultName, string initialFolder) =>
        Save(defaultName, initialFolder, "PDF document", "pdf");

    public string? SaveCsvAs(string defaultName, string initialFolder) =>
        Save(defaultName, initialFolder, "CSV file", "csv");

    public string? PickFolder(string title, string? initialFolder)
    {
        var start = !string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder) ? initialFolder : null;
        var picked = Window.ShowOpenFolder(title, start, false);
        return picked is { Length: > 0 } && !string.IsNullOrEmpty(picked[0]) ? picked[0] : null;
    }

    // a fresh process reads the new data folder from the pointer file
    public void Restart()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
        Window.Close();
    }

    Photino.NET.PhotinoWindow Window => window.Window ?? throw new InvalidOperationException("The window isn't open yet.");

    // photino's save dialog only takes a folder, so the suggested name is shown in the title
    string? Save(string defaultName, string initialFolder, string kind, string extension)
    {
        Directory.CreateDirectory(initialFolder);
        var chosen = Window.ShowSaveFile($"Save {defaultName}", initialFolder, [(kind, [extension])]);
        if (string.IsNullOrEmpty(chosen)) return null;
        return Path.HasExtension(chosen) ? chosen : chosen + "." + extension;
    }

    void Open(string target) => run(os switch
    {
        DialogOs.Mac => new("open", [target]),
        DialogOs.Windows => new("explorer.exe", [target]),
        _ => new("xdg-open", [target]),
    });

    static void Start(SystemDialog.Command command)
    {
        var start = new ProcessStartInfo(command.File) { UseShellExecute = false };
        foreach (var arg in command.Args) start.ArgumentList.Add(arg);
        Process.Start(start)?.Dispose();
    }
}
