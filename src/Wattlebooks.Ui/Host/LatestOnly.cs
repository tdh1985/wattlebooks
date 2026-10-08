// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Ui.Host;

// loads can finish out of order so only the newest one may reach the screen
public sealed class LatestOnly
{
    int _latest;

    public async Task<bool> RunAsync<T>(Func<Task<T>> load, Action<T> apply)
    {
        var ticket = Interlocked.Increment(ref _latest);
        var result = await load();
        if (ticket != Volatile.Read(ref _latest)) return false;
        apply(result);
        return true;
    }
}
