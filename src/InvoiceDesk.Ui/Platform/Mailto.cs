// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Ui.Platform;

public static class Mailto
{
    // long mailto links get cut off by some mail apps and browsers
    public const int BodyLimit = 1800;

    public static string Url(EmailDraft draft)
    {
        var body = draft.Body.Length > BodyLimit ? draft.Body[..BodyLimit] + "…" : draft.Body;
        return $"mailto:{Uri.EscapeDataString(draft.To)}?subject={Uri.EscapeDataString(draft.Subject)}" +
               $"&body={Uri.EscapeDataString(body.Replace("\n", "\r\n"))}";
    }
}
