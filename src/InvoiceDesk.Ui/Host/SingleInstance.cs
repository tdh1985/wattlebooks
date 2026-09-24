// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace InvoiceDesk.Ui.Host;

// one window per data folder, so two copies never write the same database
public sealed class SingleInstance : IDisposable
{
    readonly Mutex _mutex;
    // named events only exist on windows, elsewhere the handoff file is the signal
    readonly EventWaitHandle? _activate;
    readonly string _handoff;
    RegisteredWaitHandle? _registration;
    FileSystemWatcher? _watcher;

    public SingleInstance(string dataRoot, string localRoot) : this(dataRoot, localRoot, !OperatingSystem.IsWindows()) { }

    public SingleInstance(string dataRoot, string localRoot, bool fileSignal)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataRoot.ToLowerInvariant())))[..12];
        _mutex = new Mutex(true, $@"Local\InvoiceDesk.{key}", out var created);
        IsFirst = created;
        if (!fileSignal) _activate = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\InvoiceDesk.{key}.Activate");
        // kept on this pc, never in a synced data folder where another pc could read it
        _handoff = Path.Combine(localRoot, $"activate-{key}.txt");
    }

    public bool IsFirst { get; }

    // a jump list click in a second copy passes its page to the first before it quits
    public void SignalFirst(string? route)
    {
        // with no named event the file itself is the signal, so it's written even without a page
        if (route is not null || _activate is null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_handoff)!);
                // written aside then renamed, so a watcher never sees it half written
                var partial = _handoff + ".partial";
                File.WriteAllText(partial, route ?? "");
                File.Move(partial, _handoff, overwrite: true);
            }
            catch (IOException ex) { FileLog.Write(ex, "activation handoff"); }
            catch (UnauthorizedAccessException ex) { FileLog.Write(ex, "activation handoff"); }
        }
        _activate?.Set();
    }

    public void ListenForActivation(Action<string?> onActivate)
    {
        if (_activate is not null)
        {
            _registration = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(TakeHandoff()), null, Timeout.Infinite, executeOnlyOnce: false);
            return;
        }
        // one write can raise several events, only the one that finds the file counts
        void OnFile(object? sender, FileSystemEventArgs e)
        {
            if (File.Exists(_handoff)) onActivate(TakeHandoff());
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_handoff)!);
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_handoff)!, Path.GetFileName(_handoff));
            _watcher.Created += OnFile;
            _watcher.Changed += OnFile;
            _watcher.Renamed += OnFile;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // linux caps how many folders can be watched, a second copy then just can't bring this forward
            FileLog.Write(ex, "activation watcher");
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    string? TakeHandoff()
    {
        try
        {
            if (!File.Exists(_handoff)) return null;
            var route = File.ReadAllText(_handoff).Trim();
            File.Delete(_handoff);
            return route.Length > 0 ? route : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _watcher?.Dispose();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _activate?.Dispose();
    }
}
