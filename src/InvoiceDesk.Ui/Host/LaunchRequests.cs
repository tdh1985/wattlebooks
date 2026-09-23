// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Ui.Host;

// a page asked for from the taskbar, held until the ui is ready to open it
public sealed class LaunchRequests
{
    readonly Lock _gate = new();
    string? _pending;

    public event Action? Requested;

    public void Request(string? route)
    {
        if (route is null) return;
        lock (_gate) _pending = route;
        Requested?.Invoke();
    }

    public string? TakePending()
    {
        lock (_gate)
        {
            var route = _pending;
            _pending = null;
            return route;
        }
    }
}
