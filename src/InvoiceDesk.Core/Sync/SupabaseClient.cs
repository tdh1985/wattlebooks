// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InvoiceDesk.Core.Sync;

public class SyncException(string message, bool signedOut = false, int? status = null) : Exception(message)
{
    // the saved login no longer works, so only signing in again helps
    public bool SignedOut { get; } = signedOut;
    public int? Status { get; } = status;
}

// plain http keeps the sdk and its dependencies out of the desktop app
public sealed class SupabaseClient(HttpClient http, SyncConfig config, SyncSettings settings)
{
    // refresh tokens work once, so two refreshing together would sign one out
    readonly SemaphoreSlim _refreshing = new(1, 1);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public bool IsConfigured => config.IsSet;

    public async Task SendCodeAsync(string email)
    {
        using var reply = await SendAsync(HttpMethod.Post, "/auth/v1/otp", new { email, create_user = true }, auth: false);
        await EnsureAsync(reply, "Couldn't send the code");
    }

    public async Task<SyncSession> VerifyAsync(string email, string code)
    {
        using var reply = await SendAsync(HttpMethod.Post, "/auth/v1/verify", new { type = "email", email, token = code }, auth: false);
        if (reply.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new SyncException("That code didn't work. Check it, or send a new one.");
        await EnsureAsync(reply, "Couldn't sign in");
        return ReadSession(await reply.Content.ReadAsStringAsync(), email);
    }

    public async Task SignOutAsync()
    {
        try
        {
            using var reply = await SendAsync(HttpMethod.Post, "/auth/v1/logout", null, auth: true);
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
    }

    public async Task<JsonArray> GetAsync(string pathAndQuery)
    {
        using var reply = await AuthedAsync(HttpMethod.Get, "/rest/v1/" + pathAndQuery, null, null);
        await EnsureAsync(reply, "Couldn't read from sync");
        return JsonNode.Parse(await reply.Content.ReadAsStringAsync()) as JsonArray ?? [];
    }

    public async Task<JsonArray> WriteAsync(HttpMethod method, string pathAndQuery, object? body, string? prefer = null)
    {
        using var reply = await AuthedAsync(method, "/rest/v1/" + pathAndQuery, body, prefer);
        await EnsureAsync(reply, "Couldn't save to sync");
        var text = await reply.Content.ReadAsStringAsync();
        return text.Length == 0 ? [] : JsonNode.Parse(text) as JsonArray ?? [];
    }

    public async Task<byte[]> DownloadAsync(string bucket, string path)
    {
        using var reply = await AuthedAsync(HttpMethod.Get, $"/storage/v1/object/authenticated/{bucket}/{path}", null, null);
        await EnsureAsync(reply, "Couldn't download a photo");
        return await reply.Content.ReadAsByteArrayAsync();
    }

    public async Task<List<string>> ListFilesAsync(string bucket, string folder)
    {
        using var reply = await AuthedAsync(HttpMethod.Post, $"/storage/v1/object/list/{bucket}", new { prefix = folder, limit = 1000 }, null);
        await EnsureAsync(reply, "Couldn't list photos");
        var items = JsonNode.Parse(await reply.Content.ReadAsStringAsync()) as JsonArray ?? [];
        return items.Select(i => i?["name"]?.GetValue<string>()).OfType<string>().Select(n => $"{folder}/{n}").ToList();
    }

    public async Task DeleteFilesAsync(string bucket, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        using var reply = await AuthedAsync(HttpMethod.Delete, $"/storage/v1/object/{bucket}", new { prefixes = paths }, null);
        await EnsureAsync(reply, "Couldn't remove a photo");
    }

    async Task<HttpResponseMessage> AuthedAsync(HttpMethod method, string path, object? body, string? prefer)
    {
        var used = settings.Session?.AccessToken;
        var reply = await SendAsync(method, path, body, auth: true, prefer);
        if (reply.StatusCode != HttpStatusCode.Unauthorized) return reply;
        reply.Dispose();
        await _refreshing.WaitAsync();
        try
        {
            if (settings.Session?.AccessToken == used) await RefreshAsync();
        }
        finally
        {
            _refreshing.Release();
        }
        return await SendAsync(method, path, body, auth: true, prefer);
    }

    async Task RefreshAsync()
    {
        var (current, version) = settings.Snapshot();
        var session = current ?? throw new SyncException("Sign in to sync with your phone.", signedOut: true);
        using var reply = await SendAsync(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token",
            new { refresh_token = session.RefreshToken }, auth: false);
        if (reply.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SyncException("Your phone sync login has expired. Sign in again.", signedOut: true);
        await EnsureAsync(reply, "Couldn't renew the login");
        var renewed = ReadSession(await reply.Content.ReadAsStringAsync(), session.Email) with { Linked = session.Linked };
        if (!settings.SaveIf(version, renewed)) throw new SyncException("Phone sync was signed out.", signedOut: true);
    }

    // a request can't be sent twice, so the retry after a refresh builds a new one
    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, bool auth, string? prefer = null)
    {
        if (!config.IsSet) throw new SyncException("Phone sync isn't set up in this version.");
        var request = new HttpRequestMessage(method, config.Url + path);
        request.Headers.Add("apikey", config.Key);
        if (auth && settings.Session is { } session)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (prefer is not null) request.Headers.Add("Prefer", prefer);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        return await http.SendAsync(request);
    }

    static SyncSession ReadSession(string json, string email)
    {
        var node = JsonNode.Parse(json);
        var access = node?["access_token"]?.GetValue<string>();
        var refresh = node?["refresh_token"]?.GetValue<string>();
        var userId = node?["user"]?["id"]?.GetValue<string>();
        if (access is null || refresh is null || userId is null) throw new SyncException("Sign in came back incomplete. Try again.");
        return new SyncSession(node?["user"]?["email"]?.GetValue<string>() ?? email, userId, access, refresh);
    }

    static async Task EnsureAsync(HttpResponseMessage reply, string what)
    {
        if (reply.IsSuccessStatusCode) return;
        if (reply.StatusCode == HttpStatusCode.TooManyRequests)
            throw new SyncException($"{what}: too many tries. Wait a minute and try again.");
        var detail = await reply.Content.ReadAsStringAsync();
        throw new SyncException($"{what} ({(int)reply.StatusCode}). {Trim(detail)}".Trim(), status: (int)reply.StatusCode);
    }

    static string Trim(string text) => text.Length > 200 ? text[..200] : text;
}
