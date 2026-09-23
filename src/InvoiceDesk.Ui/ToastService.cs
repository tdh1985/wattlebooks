// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Host;
using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Ui;

public enum ToastKind { Success, Info, Error }

public sealed record ToastAction(string Label, Func<Task> Run);

public sealed class Toast
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Message { get; init; }
    public required ToastKind Kind { get; init; }
    public IReadOnlyList<ToastAction> Actions { get; init; } = [];
}

public sealed class ToastService
{
    static readonly TimeSpan PlainLife = TimeSpan.FromSeconds(4.5);
    static readonly TimeSpan ActionLife = TimeSpan.FromSeconds(8);
    static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(6);

    readonly Lock _gate = new();
    readonly List<Toast> _items = [];
    readonly Dictionary<Guid, Func<Task>> _pendingCommits = [];
    readonly Dictionary<Guid, string> _pendingKeys = [];

    public event Action? Changed;

    public IReadOnlyList<Toast> Items
    {
        get { lock (_gate) return _items.ToList(); }
    }

    public void Show(string message, ToastKind kind = ToastKind.Success, params ToastAction[] actions)
    {
        var toast = new Toast { Message = message, Kind = kind, Actions = actions };
        Add(toast);
        _ = ExpireAsync(toast.Id, actions.Length > 0 || kind == ToastKind.Error ? ActionLife : PlainLife);
    }

    public void Error(Exception ex)
    {
        if (ex is ValidationException v)
        {
            Show(string.Join(" ", v.Errors), ToastKind.Error);
            return;
        }
        FileLog.Write(ex, "ui action");
        Show($"That didn't work: {ex.Message}", ToastKind.Error);
    }

    // the item hides at once but is only deleted when the undo window closes
    public void ShowUndo(string message, string key, Func<Task> commit)
    {
        Toast? toast = null;
        toast = new Toast
        {
            Message = message,
            Kind = ToastKind.Info,
            Actions =
            [
                new ToastAction("Undo", () =>
                {
                    lock (_gate)
                    {
                        _pendingCommits.Remove(toast!.Id);
                        _pendingKeys.Remove(toast.Id);
                    }
                    Dismiss(toast!.Id);
                    return Task.CompletedTask;
                }),
            ],
        };
        lock (_gate)
        {
            _pendingCommits[toast.Id] = commit;
            _pendingKeys[toast.Id] = key;
        }
        Add(toast);
        _ = CommitLaterAsync(toast.Id);
    }

    // lists hide anything that's waiting out its undo window
    public bool IsPending(string key)
    {
        lock (_gate) return _pendingKeys.ContainsValue(key);
    }

    public void Dismiss(Guid id)
    {
        lock (_gate) _items.RemoveAll(t => t.Id == id);
        Changed?.Invoke();
    }

    public async Task FlushAsync()
    {
        List<Func<Task>> commits;
        lock (_gate)
        {
            commits = _pendingCommits.Values.ToList();
            _pendingCommits.Clear();
            _pendingKeys.Clear();
        }
        foreach (var commit in commits)
        {
            try { await commit(); }
            catch (Exception ex) { FileLog.Write(ex, "pending delete"); }
        }
    }

    void Add(Toast toast)
    {
        lock (_gate)
        {
            _items.Add(toast);
            if (_items.Count > 4) _items.RemoveAt(0);
        }
        Changed?.Invoke();
    }

    async Task ExpireAsync(Guid id, TimeSpan after)
    {
        await Task.Delay(after).ConfigureAwait(false);
        Dismiss(id);
    }

    async Task CommitLaterAsync(Guid id)
    {
        await Task.Delay(UndoWindow).ConfigureAwait(false);
        Func<Task>? commit;
        lock (_gate)
        {
            if (!_pendingCommits.Remove(id, out commit)) commit = null;
        }
        if (commit is null)
        {
            // undo or a flush already dealt with it but the toast is still up
            Dismiss(id);
            return;
        }
        try { await commit(); }
        catch (Exception ex) { Error(ex); }
        finally
        {
            lock (_gate) _pendingKeys.Remove(id);
            Dismiss(id);
        }
    }
}
