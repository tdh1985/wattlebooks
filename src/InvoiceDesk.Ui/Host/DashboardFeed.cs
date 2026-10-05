// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Services;

namespace InvoiceDesk.Ui.Host;

// one dashboard read shared by the side rail, the dashboard and the taskbar badge
public sealed class DashboardFeed : IDisposable
{
    // a burst of saves becomes one read rather than one each
    static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);

    readonly Func<Task<DashboardData>> _load;
    readonly TimeSpan _debounce;
    readonly TimeProvider _clock;
    readonly InvoiceService _invoices;
    readonly ClientService _clients;
    readonly TransactionService _ledger;
    readonly ProfileService _profiles;
    readonly System.Threading.Timer _timer;
    readonly Lock _gate = new();
    DashboardData? _current;
    DateOnly _currentDay;
    Task<DashboardData>? _loading;
    bool _stale;
    bool _disposed;

    public DashboardFeed(
        DashboardService dashboard, TimeProvider clock, InvoiceService invoices, ClientService clients,
        TransactionService ledger, ProfileService profiles)
        : this(dashboard.GetAsync, Debounce, clock, invoices, clients, ledger, profiles)
    {
    }

    internal DashboardFeed(
        Func<Task<DashboardData>> load, TimeSpan debounce, TimeProvider clock, InvoiceService invoices,
        ClientService clients, TransactionService ledger, ProfileService profiles)
    {
        _load = load;
        _debounce = debounce;
        _clock = clock;
        _invoices = invoices;
        _clients = clients;
        _ledger = ledger;
        _profiles = profiles;
        _timer = new System.Threading.Timer(_ => OnDue());
        _invoices.Changed += OnChanged;
        _clients.Changed += OnChanged;
        _ledger.Changed += OnChanged;
        _profiles.Changed += OnChanged;
    }

    // raised on a pool thread once a fresh snapshot has landed
    public event Action? Updated;

    public Task<DashboardData> GetAsync()
    {
        lock (_gate)
        {
            if (_loading is not null) return _loading;
            // invoices tip into overdue at midnight without anything being saved
            if (_current is not null && !_stale && _currentDay == _clock.Today()) return Task.FromResult(_current);
            return StartLoad();
        }
    }

    // for changes no store reports, like the clock passing a due date
    public void Invalidate() => OnChanged();

    void OnChanged()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _stale = true;
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    void OnDue()
    {
        Task<DashboardData> load;
        lock (_gate)
        {
            if (_disposed || !_stale || _loading is not null) return;
            load = StartLoad();
        }
        // the loop has logged any failure so this only marks it observed
        load.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    // the caller holds the gate
    Task<DashboardData> StartLoad()
    {
        var done = new TaskCompletionSource<DashboardData>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loading = done.Task;
        _stale = false;
        _ = Task.Run(() => LoadAsync(done));
        return done.Task;
    }

    async Task LoadAsync(TaskCompletionSource<DashboardData> done)
    {
        while (true)
        {
            var day = _clock.Today();
            DashboardData data;
            try
            {
                data = await _load();
            }
            catch (Exception ex)
            {
                FileLog.Write(ex, "dashboard load");
                lock (_gate)
                {
                    _loading = null;
                    // the next caller or change tries again
                    _stale = true;
                }
                done.TrySetException(ex);
                return;
            }

            lock (_gate)
            {
                // a save landed mid-read so read once more and let the last change win
                if (_stale && !_disposed)
                {
                    _stale = false;
                    continue;
                }
                _current = data;
                _currentDay = day;
                _loading = null;
            }
            done.TrySetResult(data);
            Updated?.Invoke();
            return;
        }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        _invoices.Changed -= OnChanged;
        _clients.Changed -= OnChanged;
        _ledger.Changed -= OnChanged;
        _profiles.Changed -= OnChanged;
        _timer.Dispose();
    }
}
