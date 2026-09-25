// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Sync;

namespace InvoiceDesk.Ui.Host;

public sealed record SyncStatus(DateTime? LastSync, string? Error, bool NeedsSignIn);

// runs only while signed in, so the app still never goes online by itself
public sealed class SyncRunner(
    SyncAccount account, SnapshotPusher pusher, InboxPuller puller, PhoneItemService phone,
    InvoiceService invoices, ClientService clients, ProfileService profiles, TransactionService ledger,
    ToastService toasts, TimeProvider clock)
{
    static readonly TimeSpan Tick = TimeSpan.FromSeconds(15);
    static readonly TimeSpan PullEvery = TimeSpan.FromSeconds(60);
    // a few seconds lets a burst of edits go up as one push
    static readonly TimeSpan PushDelay = TimeSpan.FromSeconds(3);
    static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(30);
    static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(10);

    readonly SemaphoreSlim _running = new(1, 1);
    PeriodicTimer? _timer;
    DateTimeOffset _nextPull = DateTimeOffset.MinValue;
    DateTimeOffset? _pushDue = DateTimeOffset.MinValue;
    int _failures;

    public SyncStatus Status { get; private set; } = new(null, null, false);
    public event Action? Changed;

    public static TimeSpan RetryAfter(int failures) =>
        failures <= 0 ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Min(LongestRetry.Ticks, FirstRetry.Ticks << Math.Min(failures - 1, 10)));

    public void Start()
    {
        invoices.Changed += MarkDirty;
        clients.Changed += MarkDirty;
        profiles.Changed += MarkDirty;
        ledger.Changed += MarkDirty;
        account.Changed += Kick;
        _timer = new PeriodicTimer(Tick);
        _ = Task.Run(async () =>
        {
            await RunAsync();
            while (await _timer.WaitForNextTickAsync()) await RunAsync();
        });
    }

    public void Stop()
    {
        _timer?.Dispose();
        invoices.Changed -= MarkDirty;
        clients.Changed -= MarkDirty;
        profiles.Changed -= MarkDirty;
        ledger.Changed -= MarkDirty;
        account.Changed -= Kick;
    }

    // signing in or coming back to the window shouldn't wait for the next tick
    public void Kick()
    {
        _failures = 0;
        _nextPull = DateTimeOffset.MinValue;
        _pushDue = DateTimeOffset.MinValue;
        if (account.IsOn) Status = Status with { NeedsSignIn = false };
        _ = Task.Run(RunAsync);
    }

    void MarkDirty() => _pushDue ??= clock.GetUtcNow() + PushDelay;

    async Task RunAsync()
    {
        if (!account.IsOn || Status.NeedsSignIn) return;
        var now = clock.GetUtcNow();
        if (now < _nextPull && (_pushDue is not { } due || now < due)) return;
        if (!await _running.WaitAsync(0)) return;
        try
        {
            if (_pushDue is not null)
            {
                _pushDue = null;
                await pusher.PushAsync();
            }
            if (now >= _nextPull)
            {
                var arrived = await puller.PullAsync();
                if (arrived > 0)
                {
                    toasts.Show(arrived == 1 ? "1 item arrived from your phone." : $"{arrived} items arrived from your phone.", ToastKind.Info);
                    if (await phone.CountTrayAsync() > 0) Changed?.Invoke();
                }
            }
            _failures = 0;
            _nextPull = clock.GetUtcNow() + PullEvery;
            Status = new SyncStatus(clock.Now(), null, false);
        }
        catch (SyncException ex) when (ex.SignedOut)
        {
            Status = Status with { Error = ex.Message, NeedsSignIn = true };
        }
        catch (Exception ex)
        {
            _failures++;
            _nextPull = clock.GetUtcNow() + RetryAfter(_failures);
            // a failed push waits out the same backoff rather than every tick
            _pushDue = _nextPull;
            Status = Status with
            {
                Error = ex switch
                {
                    SyncException sync => sync.Message,
                    HttpRequestException or TaskCanceledException => "Couldn't reach sync. It'll try again shortly.",
                    _ => "Something went wrong syncing. It'll try again shortly.",
                },
            };
            FileLog.Write(ex, "phone sync");
        }
        finally
        {
            _running.Release();
        }
        Changed?.Invoke();
    }
}
