// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Tmds.DBus.Protocol;

namespace InvoiceDesk.Desktop.Platform;

// the freedesktop portal passes an open file, so a sandboxed app can read what its path can't
public sealed class DesktopPortal : IFilePortal
{
    static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    public bool OpenFile(string path) => Call("OpenFile", path);

    // opens the file manager with the file already selected
    public bool OpenDirectory(string path) => Call("OpenDirectory", path);

    static bool Call(string member, string path)
    {
        try
        {
            return CallAsync(member, path).Wait(Limit);
        }
        // any failure just means xdg-open gets a turn instead
        catch (Exception ex)
        {
            Ui.Host.FileLog.Write(ex, "desktop portal");
            return false;
        }
    }

    static async Task CallAsync(string member, string path)
    {
        using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read);
        var bus = DBusConnection.Session ?? throw new IOException("There's no session bus.");
        await bus.ConnectAsync();
        await bus.CallMethodAsync(Message(bus, member, file));
    }

    // a message writer can't live across an await, so the call is built here
    static MessageBuffer Message(DBusConnection bus, string member, Microsoft.Win32.SafeHandles.SafeFileHandle file)
    {
        using var writer = bus.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.portal.Desktop", "/org/freedesktop/portal/desktop",
            "org.freedesktop.portal.OpenURI", member, "sha{sv}", MessageFlags.None);
        writer.WriteString("");
        writer.WriteHandle(file);
        writer.WriteDictionaryEnd(writer.WriteDictionaryStart());
        return writer.CreateMessage();
    }
}
