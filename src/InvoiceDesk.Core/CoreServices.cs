// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceDesk.Core;

public static class CoreServices
{
    // one place builds it so tests can release this database's pooled handles and no other
    public static string ConnectionString(AppPaths paths) => $"Data Source={paths.Database};Foreign Keys=True";

    public static IServiceCollection AddInvoiceDeskCore(this IServiceCollection services, AppPaths paths, string newDataCountry = "AU")
    {
        services.AddSingleton(paths);
        services.AddSingleton(new NewDataCountry(Countries.FromRegion(newDataCountry)));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        // split queries so loading lines with payments never multiplies rows
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(
            ConnectionString(paths),
            sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddSingleton<AttachmentStore>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<DataMover>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<ClientService>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<InvoiceService>();
        services.AddSingleton<TransactionService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<InsightService>();
        services.AddSingleton<SuggestionService>();
        services.AddSingleton<StatementService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<RecurringService>();
        services.TryAddSingleton(SyncConfig.Default());
        services.AddSingleton<SyncSettings>();
        services.AddSingleton(sp => new SupabaseClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, sp.GetRequiredService<SyncConfig>(), sp.GetRequiredService<SyncSettings>()));
        services.AddSingleton<SyncAccount>();
        services.AddSingleton<PhoneItemService>();
        services.AddSingleton<SnapshotPusher>();
        services.AddSingleton<InboxPuller>();
        return services;
    }
}
