// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Text.Json.Nodes;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Core.Sync;

public enum LinkResult { Linked, OtherBusiness }

// signing in, and making sure one login only ever carries one business
public sealed class SyncAccount(SupabaseClient api, SyncSettings settings, IDbContextFactory<AppDbContext> factory)
{
    static readonly string[] Tables = ["inbox", "snap_invoices", "snap_clients", "snap_profile", "sync_links"];

    public event Action? Changed;

    public bool IsConfigured => api.IsConfigured;
    public SyncSession? Session => settings.Session;
    public bool IsOn => Session is { Linked: true };

    public Task SendCodeAsync(string email)
    {
        email = email.Trim();
        if (!email.Contains('@') || email.Length < 3) throw new SyncException("Enter the email address to sign in with.");
        return api.SendCodeAsync(email);
    }

    public async Task<LinkResult> VerifyAsync(string email, string code)
    {
        code = new string(code.Where(char.IsDigit).ToArray());
        if (code.Length < 6) throw new SyncException("Enter the code from the email.");
        settings.Save(await api.VerifyAsync(email.Trim(), code));
        return await LinkAsync(takeOver: false);
    }

    // taking over wipes the phone view so the other business's invoices vanish from it
    public async Task<LinkResult> LinkAsync(bool takeOver)
    {
        var session = settings.Session ?? throw new SyncException("Sign in to sync with your phone.", signedOut: true);
        var mine = await SyncIdAsync();
        var existing = (await api.GetAsync("sync_links?select=sync_id")).OfType<JsonObject>()
            .Select(r => r["sync_id"]?.GetValue<string>()).FirstOrDefault();
        if (existing is not null && existing != mine && !takeOver) return LinkResult.OtherBusiness;

        if (existing != mine)
        {
            if (existing is not null)
                foreach (var table in new[] { "snap_invoices", "snap_clients", "snap_profile" })
                    await api.WriteAsync(HttpMethod.Delete, $"{table}?user_id=eq.{session.UserId}", null, "return=minimal");
            await api.WriteAsync(HttpMethod.Post, "sync_links?on_conflict=user_id",
                new { user_id = session.UserId, sync_id = mine, linked_at = DateTimeOffset.UtcNow },
                "resolution=merge-duplicates,return=minimal");
        }
        settings.Save(session with { Linked = true });
        Changed?.Invoke();
        return LinkResult.Linked;
    }

    public async Task SignOutAsync(bool removeData)
    {
        if (settings.Session is { } session && removeData)
        {
            var photos = await api.ListFilesAsync(InboxPuller.Bucket, session.UserId);
            await api.DeleteFilesAsync(InboxPuller.Bucket, photos);
            foreach (var table in Tables)
                await api.WriteAsync(HttpMethod.Delete, $"{table}?user_id=eq.{session.UserId}", null, "return=minimal");
        }
        await api.SignOutAsync();
        settings.Save(null);
        Changed?.Invoke();
    }

    // made on first use so databases from before sync get one too
    async Task<string> SyncIdAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var profile = await db.Profiles.SingleAsync(p => p.Id == BusinessProfile.SingletonId);
        if (profile.SyncId.Length == 0)
        {
            profile.SyncId = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }
        return profile.SyncId;
    }
}
