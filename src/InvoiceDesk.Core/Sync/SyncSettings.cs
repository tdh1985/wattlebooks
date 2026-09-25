// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.Json;
using InvoiceDesk.Core.Storage;

namespace InvoiceDesk.Core.Sync;

// linked is false until this login is tied to this database
public sealed record SyncSession(string Email, string UserId, string AccessToken, string RefreshToken, bool Linked = false);

public sealed record SyncState(SyncSession? Session, string DeviceId);

// kept beside prefs on this pc so a login never travels in a synced data folder
public sealed class SyncSettings(AppPaths paths)
{
    readonly Lock _gate = new();
    SyncState? _state;
    long _version;

    string File => Path.Combine(paths.LocalRoot, "sync.json");

    public SyncState State
    {
        get
        {
            lock (_gate) return _state ??= Load();
        }
    }

    public SyncSession? Session => State.Session;

    // a refresh started before a sign out must not bring the old login back
    public (SyncSession? Session, long Version) Snapshot()
    {
        lock (_gate) return (State.Session, _version);
    }

    public bool SaveIf(long version, SyncSession session)
    {
        lock (_gate)
        {
            if (version != _version) return false;
            Save(session);
            return true;
        }
    }

    public void Save(SyncSession? session)
    {
        lock (_gate)
        {
            _version++;
            _state = State with { Session = session };
            Directory.CreateDirectory(paths.LocalRoot);
            var temp = File + ".tmp";
            System.IO.File.WriteAllText(temp, JsonSerializer.Serialize(_state));
            System.IO.File.Move(temp, File, overwrite: true);
        }
    }

    SyncState Load()
    {
        try
        {
            if (System.IO.File.Exists(File)
                && JsonSerializer.Deserialize<SyncState>(System.IO.File.ReadAllText(File)) is { DeviceId.Length: > 0 } state)
                return state;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new SyncState(null, Guid.NewGuid().ToString("N"));
    }
}
