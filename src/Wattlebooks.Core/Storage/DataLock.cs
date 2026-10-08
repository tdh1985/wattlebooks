// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.Json;

namespace Wattlebooks.Core.Storage;

public sealed record LockInfo(string Machine, string User, int ProcessId, DateTimeOffset StartedAt, DateTimeOffset Heartbeat);

// best effort only: the lock file itself takes a while to sync between pcs
public sealed class DataLock(AppPaths paths, TimeProvider clock, string machine, string user, int processId)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(3);

    DateTimeOffset _startedAt;

    public static DataLock ForThisProcess(AppPaths paths, TimeProvider clock) =>
        new(paths, clock, Environment.MachineName, Environment.UserName, Environment.ProcessId);

    public LockInfo? ReadOther()
    {
        var info = Read();
        if (info is null) return null;
        // a lock from this pc can only be a leftover from a crash
        if (string.Equals(info.Machine, machine, StringComparison.OrdinalIgnoreCase)) return null;
        return clock.GetUtcNow() - info.Heartbeat > StaleAfter ? null : info;
    }

    public void Acquire()
    {
        _startedAt = clock.GetUtcNow();
        Write();
    }

    public void Heartbeat()
    {
        var current = Read();
        if (current is null || IsOurs(current)) Write();
    }

    public void Release()
    {
        var current = Read();
        if (current is null || !IsOurs(current)) return;
        try { File.Delete(paths.LockFile); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    bool IsOurs(LockInfo info) =>
        string.Equals(info.Machine, machine, StringComparison.OrdinalIgnoreCase) && info.ProcessId == processId;

    void Write()
    {
        var now = clock.GetUtcNow();
        var info = new LockInfo(machine, user, processId, _startedAt == default ? now : _startedAt, now);
        var temp = paths.LockFile + ".tmp";
        try
        {
            Directory.CreateDirectory(paths.DataRoot);
            File.WriteAllText(temp, JsonSerializer.Serialize(info));
            File.Move(temp, paths.LockFile, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    LockInfo? Read()
    {
        try
        {
            return File.Exists(paths.LockFile) ? JsonSerializer.Deserialize<LockInfo>(File.ReadAllText(paths.LockFile)) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
