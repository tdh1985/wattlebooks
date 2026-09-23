// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Ui.Platform;

public interface IReceiptReader
{
    // false where the os has no text reader, so scanning is hidden
    bool IsAvailable { get; }

    Task<string?> ReadTextAsync(string path);
}
