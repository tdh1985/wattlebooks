// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Storage;

// data can live in a synced folder but caches and prefs stay on this pc
public sealed class AppPaths
{
    public const string DatabaseFileName = "invoicedesk.db";

    public AppPaths(string dataRoot, string localRoot, string? homeRoot = null)
    {
        DataRoot = Path.GetFullPath(dataRoot);
        LocalRoot = Path.GetFullPath(localRoot);
        HomeRoot = Path.GetFullPath(homeRoot ?? localRoot);
    }

    public AppPaths(string root) : this(root, root)
    {
    }

    public string DataRoot { get; }
    public string LocalRoot { get; }
    // where the data lives until it's moved
    public string HomeRoot { get; }
    public bool IsCustomLocation => !string.Equals(DataRoot, HomeRoot, StringComparison.OrdinalIgnoreCase);

    public string Database => Path.Combine(DataRoot, DatabaseFileName);
    public string Attachments => Path.Combine(DataRoot, "attachments");
    string? _exports;

    public string Exports => _exports ?? Path.Combine(DataRoot, "exports");

    // linux keeps exports where sandboxed apps such as snaps can open them
    public AppPaths WithExports(string folder) => new(DataRoot, LocalRoot, HomeRoot) { _exports = Path.GetFullPath(folder) };
    public string Backups => Path.Combine(DataRoot, "backups");
    public string LockFile => Path.Combine(DataRoot, "invoicedesk.lock");

    public string Logs => Path.Combine(LocalRoot, "logs");
    public string Staging => Path.Combine(LocalRoot, "staging");
    public string Render => Path.Combine(LocalRoot, "render");
    public string WebView => Path.Combine(LocalRoot, "webview");
    public string Prefs => Path.Combine(LocalRoot, "prefs.json");

    public static string LocalDefault() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceDesk");

    // INVOICEDESK_LOCAL or INVOICEDESK_DATA point test runs at scratch folders
    public static AppPaths Default()
    {
        var local = Environment.GetEnvironmentVariable("INVOICEDESK_LOCAL");
        if (!string.IsNullOrWhiteSpace(local)) return DataLocation.Resolve(local);
        var data = Environment.GetEnvironmentVariable("INVOICEDESK_DATA");
        if (!string.IsNullOrWhiteSpace(data)) return new AppPaths(data);
        return DataLocation.Resolve(LocalDefault(), StoreHome());
    }

    // the store virtualises appdata writes and wipes them on uninstall, so store installs keep the books in documents
    static string? StoreHome() =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "AppxManifest.xml"))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InvoiceDesk")
            : null;

    public void EnsureCreated()
    {
        foreach (var dir in new[] { DataRoot, Attachments, Exports, Backups, LocalRoot, Logs, Staging, Render, WebView })
            Directory.CreateDirectory(dir);
    }

    public string FullPath(string relativePath) =>
        Path.Combine(DataRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
