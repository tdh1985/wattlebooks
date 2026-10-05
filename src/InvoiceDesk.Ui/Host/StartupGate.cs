// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Ui.Host;

// pages wait on this so the window can open while the data is set up
public sealed class StartupGate
{
    readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => _ready.Task;
    public void Open() => _ready.TrySetResult();
    public void Fail(Exception ex) => _ready.TrySetException(ex);
}
