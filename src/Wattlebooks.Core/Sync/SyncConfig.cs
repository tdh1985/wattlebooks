// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace Wattlebooks.Core.Sync;

// the publishable key is meant to ship in apps, row level security guards the data
public sealed record SyncConfig(string Url, string Key, string PhoneUrl = "")
{
    const string BuiltInUrl = "https://hckrulledfqdrwlltdyn.supabase.co";
    const string BuiltInKey = "sb_publishable_Qq88vo8NkiLtJxY5hc7mow_ebeQcnyH";
    const string BuiltInPhoneUrl = "https://app.wattlebooks.work";

    public bool IsSet => Url.Length > 0 && Key.Length > 0;

    // WATTLEBOOKS_SYNC_URL and WATTLEBOOKS_SYNC_KEY point dev runs at another project
    public static SyncConfig Default() => new(
        Env("WATTLEBOOKS_SYNC_URL", BuiltInUrl).TrimEnd('/'),
        Env("WATTLEBOOKS_SYNC_KEY", BuiltInKey),
        Env("WATTLEBOOKS_PHONE_URL", BuiltInPhoneUrl));

    static string Env(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
