// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.Json;

namespace InvoiceDesk.Core.Storage;

// a tiny pointer file on this pc that says which folder holds the data
public static class DataLocation
{
    public const string FileName = "location.json";

    sealed record Pointer(string DataRoot);

    public static AppPaths Resolve(string localRoot, string? homeRoot = null) =>
        new(Read(localRoot) ?? homeRoot ?? localRoot, localRoot, homeRoot);

    public static string? Read(string localRoot)
    {
        var file = Path.Combine(localRoot, FileName);
        try
        {
            if (!File.Exists(file)) return null;
            var pointer = JsonSerializer.Deserialize<Pointer>(File.ReadAllText(file));
            return string.IsNullOrWhiteSpace(pointer?.DataRoot) ? null : Path.GetFullPath(pointer.DataRoot);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string localRoot, string? dataRoot)
    {
        var file = Path.Combine(localRoot, FileName);
        if (dataRoot is null || SamePath(dataRoot, localRoot))
        {
            if (File.Exists(file)) File.Delete(file);
            return;
        }
        Directory.CreateDirectory(localRoot);
        File.WriteAllText(file, JsonSerializer.Serialize(new Pointer(Path.GetFullPath(dataRoot))));
    }

    public static bool HasData(string folder) => File.Exists(Path.Combine(folder, AppPaths.DatabaseFileName));

    public static bool SamePath(string a, string b) =>
        string.Equals(Normalise(a), Normalise(b), StringComparison.OrdinalIgnoreCase);

    public static bool IsInside(string child, string parent) =>
        Normalise(child).StartsWith(Normalise(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    static string Normalise(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
