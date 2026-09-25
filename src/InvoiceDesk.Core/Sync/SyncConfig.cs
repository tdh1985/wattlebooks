// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Sync;

// the publishable key is meant to ship in apps, row level security guards the data
public sealed record SyncConfig(string Url, string Key, string PhoneUrl = "")
{
    const string BuiltInUrl = "";
    const string BuiltInKey = "";
    const string BuiltInPhoneUrl = "";

    public bool IsSet => Url.Length > 0 && Key.Length > 0;

    // INVOICEDESK_SYNC_URL and INVOICEDESK_SYNC_KEY point dev runs at another project
    public static SyncConfig Default() => new(
        Env("INVOICEDESK_SYNC_URL", BuiltInUrl).TrimEnd('/'),
        Env("INVOICEDESK_SYNC_KEY", BuiltInKey),
        Env("INVOICEDESK_PHONE_URL", BuiltInPhoneUrl));

    static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
