// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Microsoft.Data.Sqlite;

namespace Wattlebooks.Core.Storage;

internal static class SqliteCopy
{
    // vacuum into runs as a normal statement so it retries while the file is busy
    public static void To(string sourceDb, string targetDb)
    {
        using var source = new SqliteConnection($"Data Source={sourceDb};Pooling=False");
        source.Open();
        using var command = source.CreateCommand();
        command.CommandText = "VACUUM INTO $target";
        command.Parameters.AddWithValue("$target", targetDb);
        command.ExecuteNonQuery();
    }
}
