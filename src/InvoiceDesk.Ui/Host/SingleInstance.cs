// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace InvoiceDesk.Ui.Host;

// one window per data folder, so two copies never write the same database
public sealed class SingleInstance : IDisposable
{
    readonly Mutex _mutex;
    readonly EventWaitHandle _activate;
    readonly string _handoff;
    RegisteredWaitHandle? _registration;

    public SingleInstance(string dataRoot, string localRoot)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataRoot.ToLowerInvariant())))[..12];
        _mutex = new Mutex(true, $@"Local\InvoiceDesk.{key}", out var created);
        IsFirst = created;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\InvoiceDesk.{key}.Activate");
        // kept on this pc, never in a synced data folder where another pc could read it
        _handoff = Path.Combine(localRoot, $"activate-{key}.txt");
    }

    public bool IsFirst { get; }

    // a jump list click in a second copy passes its page to the first before it quits
    public void SignalFirst(string? route)
    {
        if (route is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_handoff)!);
                File.WriteAllText(_handoff, route);
            }
            catch (IOException ex) { FileLog.Write(ex, "activation handoff"); }
            catch (UnauthorizedAccessException ex) { FileLog.Write(ex, "activation handoff"); }
        }
        _activate.Set();
    }

    public void ListenForActivation(Action<string?> onActivate) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(TakeHandoff()), null, Timeout.Infinite, executeOnlyOnce: false);

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
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _activate.Dispose();
    }
}
