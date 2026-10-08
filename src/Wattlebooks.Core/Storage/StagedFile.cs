// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Storage;

// picked in the ui but not saved yet, kept in staging for the preview
public sealed record StagedFile(string TempPath, string OriginalFileName, string RelativePath)
{
    public string ContentType => AttachmentStore.ContentTypeFor(OriginalFileName);
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}
