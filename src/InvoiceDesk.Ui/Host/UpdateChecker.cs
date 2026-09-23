// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using InvoiceDesk.Core.Rules;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Ui.Host;

public sealed record UpdateResult(bool Reached, bool IsNewer, string Latest, string? Url);

// the one place the app goes online, and only when someone clicks check
public sealed class UpdateChecker(ILogger<UpdateChecker> log)
{
    const string LatestRelease = "https://api.github.com/repos/tdh1985/invoicedesk/releases/latest";
    const string ReleasesPage = "https://github.com/tdh1985/invoicedesk/releases/";

    public static string CurrentVersion { get; } = ReleaseVersion.Display(
        typeof(UpdateChecker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0");

    public async Task<UpdateResult> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"InvoiceDesk/{CurrentVersion}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var doc = JsonDocument.Parse(await http.GetStringAsync(LatestRelease));
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var url = doc.RootElement.TryGetProperty("html_url", out var link) ? link.GetString() : null;
            // only ever open our own release pages, whatever the reply says
            if (url is null || !url.StartsWith(ReleasesPage, StringComparison.Ordinal)) url = ReleasesPage + "latest";
            return new UpdateResult(true, ReleaseVersion.IsNewer(tag, CurrentVersion), ReleaseVersion.Display(tag), url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning(ex, "Update check failed");
            return new UpdateResult(false, false, "", null);
        }
    }
}
