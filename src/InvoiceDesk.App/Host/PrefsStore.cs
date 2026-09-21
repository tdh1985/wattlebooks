// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.App.Host;

public sealed class AppPrefs
{
    public string Theme { get; set; } = "system";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool Maximized { get; set; }
    public bool Translucent { get; set; } = true;
}

// window size and theme live outside the database since they belong to this pc
public sealed class PrefsStore(AppPaths paths)
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    AppPrefs? _current;

    public AppPrefs Current => _current ??= Load();

    public void Save()
    {
        try { File.WriteAllText(paths.Prefs, JsonSerializer.Serialize(Current, Options)); }
        catch (IOException ex) { FileLog.Write(ex, "prefs save"); }
        catch (UnauthorizedAccessException ex) { FileLog.Write(ex, "prefs save"); }
    }

    AppPrefs Load()
    {
        try
        {
            if (File.Exists(paths.Prefs))
                return JsonSerializer.Deserialize<AppPrefs>(File.ReadAllText(paths.Prefs)) ?? new AppPrefs();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            FileLog.Write(ex, "prefs load");
        }
        return new AppPrefs();
    }
}
