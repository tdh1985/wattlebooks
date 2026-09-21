// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace InvoiceDesk.App.Host;

// shell and dialog calls the webview can't make on its own
public sealed class Desktop
{
    public void OpenFile(string path) => Start(path);

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Start(path);
    }

    public void ShowInFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    // web links only, so a bad string can't launch a program
    public void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            Start(uri.AbsoluteUri);
    }

    public string? SaveFileAs(string defaultName, string initialFolder) =>
        SaveFileAs(defaultName, initialFolder, "PDF document (*.pdf)|*.pdf", ".pdf");

    public string? SaveCsvAs(string defaultName, string initialFolder) =>
        SaveFileAs(defaultName, initialFolder, "CSV file for Excel (*.csv)|*.csv", ".csv");

    static string? SaveFileAs(string defaultName, string initialFolder, string filter, string extension)
    {
        Directory.CreateDirectory(initialFolder);
        var dialog = new SaveFileDialog
        {
            FileName = defaultName,
            InitialDirectory = initialFolder,
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder)) dialog.InitialDirectory = initialFolder;
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    // a fresh process reads the new data folder from the pointer file
    public void Restart()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
        Application.Current.Shutdown();
    }

    static void Start(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}
