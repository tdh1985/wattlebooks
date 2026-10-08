// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Domain;

public class Attachment
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = "";

    // relative to the data root with forward slashes so it doubles as a url path
    public string StoredPath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public AttachmentKind Kind { get; set; }
    public int? TransactionId { get; set; }
    public int? InvoiceId { get; set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}
