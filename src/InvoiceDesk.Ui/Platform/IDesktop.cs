// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Ui.Platform;

// shell and dialog calls the webview can't make on its own
public interface IDesktop
{
    void OpenFile(string path);
    void OpenFolder(string path);
    void ShowInFolder(string path);

    // web links only, so a bad string can't launch a program
    void OpenUrl(string url);

    // mailto links only, handed to whatever mail app the os uses
    void OpenMail(string mailtoUrl);

    string? SaveFileAs(string defaultName, string initialFolder);
    string? SaveCsvAs(string defaultName, string initialFolder);
    string? PickFolder(string title, string? initialFolder);

    // a fresh process reads the new data folder from the pointer file
    void Restart();
}
