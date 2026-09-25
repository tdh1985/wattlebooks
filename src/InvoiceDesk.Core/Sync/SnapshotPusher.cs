// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Domain;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;

namespace InvoiceDesk.Core.Sync;

// the phone's read-only view of who owes what, rebuilt whole each time
public sealed class SnapshotPusher(
    SupabaseClient api, SyncSettings settings, InvoiceService invoices, ClientService clients,
    ProfileService profiles, TimeProvider clock)
{
    const string Upsert = "resolution=merge-duplicates,return=minimal";
    public static readonly TimeSpan RecentlyPaid = TimeSpan.FromDays(90);

    public async Task PushAsync()
    {
        var uid = settings.Session?.UserId ?? throw new SyncException("Sign in to sync with your phone.", signedOut: true);

        var profile = await profiles.GetAsync();
        var country = Countries.For(profile.Country);
        await api.WriteAsync(HttpMethod.Post, "snap_profile?on_conflict=user_id", new
        {
            user_id = uid, business_name = profile.Name, currency = country.Currency,
            tax_enabled = profile.TaxRegistered, tax_rate_ppm = profile.TaxRatePpm, tax_name = country.TaxName,
            updated_at = clock.GetUtcNow(),
        }, Upsert);

        var clientRows = (await clients.ListAsync())
            .Select(c => new { user_id = uid, local_id = c.Client.Id, name = c.Client.Name, email = c.Client.Email })
            .ToList();
        await ReplaceAsync("snap_clients", uid, clientRows, clientRows.Select(c => c.local_id));

        var invoiceRows = (await InvoicesAsync())
            .Select(i => new
            {
                user_id = uid, local_id = i.Id, number = i.Number, client_name = i.ClientName,
                issue_date = i.IssueDate, due_date = i.DueDate, currency = i.Currency,
                total_cents = i.TotalCents, paid_cents = i.PaidCents, status = i.Status.ToString(),
            })
            .ToList();
        await ReplaceAsync("snap_invoices", uid, invoiceRows, invoiceRows.Select(i => i.local_id));
    }

    // what's owed, plus recent ones so a payment shows up as paid on the phone
    public async Task<List<InvoiceSummary>> InvoicesAsync()
    {
        var since = DateOnly.FromDateTime(clock.Now() - RecentlyPaid);
        return (await invoices.ListAsync())
            .Where(i => i.Status is not (DisplayStatus.Draft or DisplayStatus.Void))
            .Where(i => i.BalanceCents > 0 || i.IssueDate >= since)
            .ToList();
    }

    async Task ReplaceAsync<T>(string table, string uid, List<T> rows, IEnumerable<int> keep)
    {
        if (rows.Count > 0) await api.WriteAsync(HttpMethod.Post, $"{table}?on_conflict=user_id,local_id", rows, Upsert);
        var ids = string.Join(",", keep);
        var gone = ids.Length == 0 ? "" : $"&local_id=not.in.({ids})";
        await api.WriteAsync(HttpMethod.Delete, $"{table}?user_id=eq.{uid}{gone}", null, "return=minimal");
    }
}
