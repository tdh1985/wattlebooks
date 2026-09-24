// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace InvoiceDesk.Desktop.Platform;

// snap apps can't read the hidden data folder, so exported pdfs and csvs go in documents
public static partial class LinuxExports
{
    public static string Folder() => Folder(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ReadUserDirs(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    // the documents folder can be renamed or translated, user-dirs.dirs says where it is
    public static string Folder(string home, string? userDirs)
    {
        var documents = home.TrimEnd('/') + "/Documents";
        if (userDirs is not null && DocumentsLine().Match(userDirs) is { Success: true } line)
            documents = line.Groups[1].Value.Replace("$HOME", home.TrimEnd('/'));
        return documents.TrimEnd('/') + "/InvoiceDesk";
    }

    static string? ReadUserDirs(string home)
    {
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } c ? c : Path.Combine(home, ".config");
        try { return File.ReadAllText(Path.Combine(config, "user-dirs.dirs")); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    [GeneratedRegex("^XDG_DOCUMENTS_DIR=\"([^\"]+)\"", RegexOptions.Multiline)]
    private static partial Regex DocumentsLine();
}
