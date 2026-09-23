// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Ui;

// one js key listener fans out to whichever page cares
public sealed class ShortcutService
{
    readonly Lock _gate = new();
    readonly List<Overlay> _overlays = [];

    public event Action<string>? Pressed;

    public void Raise(string key)
    {
        // esc belongs to the newest overlay so a preview closes without its drawer
        if (key == "escape" && Top() is { } top)
        {
            top.Close();
            return;
        }
        Pressed?.Invoke(key);
    }

    public IDisposable AddOverlay(Action close)
    {
        var overlay = new Overlay(this, close);
        lock (_gate) _overlays.Add(overlay);
        return overlay;
    }

    Overlay? Top()
    {
        lock (_gate) return _overlays.Count > 0 ? _overlays[^1] : null;
    }

    void Remove(Overlay overlay)
    {
        lock (_gate) _overlays.Remove(overlay);
    }

    sealed class Overlay(ShortcutService owner, Action close) : IDisposable
    {
        public void Close() => close();

        public void Dispose() => owner.Remove(this);
    }
}
