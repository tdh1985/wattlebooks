// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Core.Storage;

public sealed class DataMover(AppPaths paths)
{
    // copies rather than moves so the old folder stays behind as a spare
    public void MoveTo(string target)
    {
        var destination = Path.GetFullPath(target);
        if (DataLocation.SamePath(destination, paths.DataRoot))
            throw new ValidationException("Your data is already in that folder.");
        if (DataLocation.IsInside(destination, paths.DataRoot) || DataLocation.IsInside(paths.DataRoot, destination))
            throw new ValidationException("Choose a folder that isn't inside, or around, the current data folder.");
        if (DataLocation.HasData(destination))
            throw new ValidationException("That folder already has Wattlebooks data. Use \"Use data from another PC\" to switch to it instead.");

        Directory.CreateDirectory(destination);
        var copy = new AppPaths(destination, paths.LocalRoot);
        string[] folders = [copy.Attachments, copy.Exports, copy.Backups];
        var created = folders.Where(f => !Directory.Exists(f)).ToList();
        var copied = new List<string>();
        try
        {
            CopyFolder(paths.Attachments, copy.Attachments, copied);
            CopyFolder(paths.Exports, copy.Exports, copied);
            CopyFolder(paths.Backups, copy.Backups, copied);
            // the database marks a folder as usable so it only lands once the rest has
            SqliteCopy.To(paths.Database, copy.Database);
        }
        catch
        {
            // a half copy would block a retry and could be adopted by mistake
            RemovePartialCopy([.. copied, copy.Database], created);
            throw;
        }

        DataLocation.Save(paths.LocalRoot, destination);
    }

    public void UseExisting(string folder)
    {
        var found = FindDataFolder(folder)
                    ?? throw new ValidationException("No Wattlebooks data was found there. Pick the folder that contains invoicedesk.db.");
        DataLocation.Save(paths.LocalRoot, found);
    }

    public void UseThisPc() => DataLocation.Save(paths.LocalRoot, null);

    // people often pick the parent folder, so look one level down as well
    public static string? FindDataFolder(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        if (DataLocation.HasData(folder)) return Path.GetFullPath(folder);
        var child = Path.Combine(folder, "InvoiceDesk");
        return DataLocation.HasData(child) ? Path.GetFullPath(child) : null;
    }

    static void CopyFolder(string from, string to, List<string> copied)
    {
        if (!Directory.Exists(from)) return;
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
            copied.Add(target);
        }
    }

    static void RemovePartialCopy(IEnumerable<string> files, IEnumerable<string> createdFolders)
    {
        try
        {
            foreach (var file in files)
                if (File.Exists(file)) File.Delete(file);
            foreach (var folder in createdFolders)
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
