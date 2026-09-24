// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Desktop.Platform;

// snap and flatpak apps can't read the hidden data folder, the portal hands them the file
public interface IFilePortal
{
    bool OpenFile(string path);

    bool OpenDirectory(string path);
}
