// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Wattlebooks.Ui.Platform;

namespace Wattlebooks.Desktop.Platform;

// macos and linux have no free text reader built in, so receipts are only attached
public sealed class NoReceiptReader : IReceiptReader
{
    public bool IsAvailable => false;

    public Task<string?> ReadTextAsync(string path) => Task.FromResult<string?>(null);
}
