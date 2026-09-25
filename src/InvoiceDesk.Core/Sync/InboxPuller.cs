// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Text.Json.Nodes;

namespace InvoiceDesk.Core.Sync;

// brings phone uploads down, claiming each so two pcs never both import it
public sealed class InboxPuller(SupabaseClient api, SyncSettings settings, PhoneItemService items, TimeProvider clock)
{
    public const string Bucket = "receipts";

    // a claim this old belongs to a pc that stopped halfway, so another can take it
    public static readonly TimeSpan StaleClaim = TimeSpan.FromMinutes(10);

    public async Task<int> PullAsync()
    {
        _ = settings.Session ?? throw new SyncException("Sign in to sync with your phone.", signedOut: true);
        var device = settings.State.DeviceId;
        var stale = Stamp(clock.GetUtcNow() - StaleClaim);
        var rows = await api.GetAsync("inbox?select=id,kind,payload,photo_path,claimed_at,claimed_by&order=created_at.asc&limit=50");

        var imported = 0;
        var stuck = new List<string>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            // the id goes into a url, so anything but a uuid is skipped
            if (row["id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var rawId) || !Guid.TryParse(rawId, out var guid))
                continue;
            var id = guid.ToString("D");
            var photo = row["photo_path"] is JsonValue p && p.TryGetValue<string>(out var path) ? path : null;

            // a row still here after its import only needs the cloud copy removed
            if (await items.HasAsync(id))
            {
                await RemoveAsync(id, photo);
                continue;
            }

            var claim = $"inbox?id=eq.{id}&or=(claimed_at.is.null,claimed_by.eq.{device},claimed_at.lt.{Uri.EscapeDataString(stale)})";
            var won = await api.WriteAsync(HttpMethod.Patch, claim,
                new { claimed_at = clock.GetUtcNow(), claimed_by = device }, "return=representation");
            if (won.Count == 0) continue;

            var kind = row["kind"] is JsonValue k && k.TryGetValue<string>(out var text) ? text : "";
            // a newer phone app sent something this version can't read yet
            if (kind is not ("receipt" or "draft_invoice")) continue;
            try
            {
                if (kind == "receipt") await ImportReceiptAsync(id, photo, row["payload"]);
                else await items.ImportDraftAsync(id, row["payload"]);
            }
            catch (Exception ex) when (ex is not (SyncException or HttpRequestException or TaskCanceledException))
            {
                // the cloud copy stays for the next pull, and the rest still come down
                stuck.Add(ex.Message);
                continue;
            }
            await RemoveAsync(id, photo);
            imported++;
        }
        if (stuck.Count > 0)
            throw new SyncException(stuck.Count == 1
                ? $"An item from your phone couldn't be saved on this computer. {stuck[0]}"
                : $"{stuck.Count} items from your phone couldn't be saved on this computer. {stuck[0]}");
        return imported;
    }

    async Task ImportReceiptAsync(string id, string? photo, JsonNode? payload)
    {
        byte[]? bytes = null;
        try
        {
            if (photo is { Length: > 0 }) bytes = await api.DownloadAsync(Bucket, photo);
        }
        catch (SyncException ex) when (ex.Status is 400 or 404)
        {
        }
        if (bytes is null) await items.RecordProblemAsync(id, "A receipt photo from your phone didn't arrive. Take it again.");
        else await items.ImportReceiptAsync(id, bytes, payload);
    }

    async Task RemoveAsync(string id, string? photo)
    {
        if (photo is { Length: > 0 }) await api.DeleteFilesAsync(Bucket, [photo]);
        await api.WriteAsync(HttpMethod.Delete, $"inbox?id=eq.{id}", null, "return=minimal");
    }

    static string Stamp(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
