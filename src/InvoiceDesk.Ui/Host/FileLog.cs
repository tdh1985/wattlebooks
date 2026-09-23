// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;

namespace InvoiceDesk.Ui.Host;

public static class FileLog
{
    static readonly Lock Gate = new();

    public static string Folder { get; private set; } = "";

    public static void Initialize(string folder) => Folder = folder;

    public static void Write(Exception? ex, string context)
    {
        if (ex is null) return;
        WriteMessage(context, ex);
    }

    public static void WriteMessage(string message, Exception? ex)
    {
        if (Folder.Length == 0) return;
        try
        {
            var now = DateTime.Now;
            var entry = $"[{now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            lock (Gate) File.AppendAllText(Path.Combine(Folder, $"{now:yyyy-MM-dd}.log"), entry);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
