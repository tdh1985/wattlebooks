// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Wattlebooks.Core.Domain;
using Wattlebooks.Core.Rules;
using Wattlebooks.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace Wattlebooks.Core.Data;

public sealed record NewDataCountry(string Code);

public sealed class DatabaseInitializer(
    AppPaths paths, IDbContextFactory<AppDbContext> factory, AttachmentStore store, TimeProvider clock, NewDataCountry newData)
{
    public const int BackupsToKeep = 10;

    static readonly string[] IncomeCategories = ["Sales", "Interest", "Other income"];
    static readonly string[] ExpenseCategories =
    [
        "Software & subscriptions", "Equipment", "Fuel & travel", "Office", "Phone & internet",
        "Advertising", "Professional fees", "Bank fees", "Other expense",
    ];

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        paths.EnsureCreated();
        var brandNew = !File.Exists(paths.Database);
        BackupDatabase();
        store.ClearStaging();

        await using var db = await factory.CreateDbContextAsync(ct);
        // migrate takes a lock and writes even when there is nothing to apply
        if (brandNew || (await db.Database.GetPendingMigrationsAsync(ct)).Any())
            await db.Database.MigrateAsync(ct);
        // wal keeps extra side files that cloud sync can copy half written
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;", ct);
        await SeedAsync(db, ct);
    }

    void BackupDatabase()
    {
        if (!File.Exists(paths.Database)) return;

        var target = Path.Combine(paths.Backups, $"invoicedesk-{clock.GetLocalNow():yyyyMMdd-HHmmss}.db");
        if (File.Exists(target)) File.Delete(target);

        SqliteCopy.To(paths.Database, target);

        var stale = Directory.GetFiles(paths.Backups, "invoicedesk-*.db")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(BackupsToKeep);
        foreach (var old in stale) File.Delete(old);
    }

    async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        if (!await db.Profiles.AnyAsync(ct))
        {
            var profile = new BusinessProfile();
            Countries.For(newData.Code).ApplyDefaults(profile);
            db.Profiles.Add(profile);
        }

        if (!await db.Categories.AnyAsync(ct))
        {
            db.Categories.AddRange(IncomeCategories.Select((n, i) => new Category { Name = n, Direction = Direction.In, SortOrder = i }));
            db.Categories.AddRange(ExpenseCategories.Select((n, i) => new Category { Name = n, Direction = Direction.Out, SortOrder = i }));
        }

        await db.SaveChangesAsync(ct);
    }
}
