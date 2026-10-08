// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Wattlebooks.Ui.Host;
using Wattlebooks.Ui.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace Wattlebooks.Ui;

// each host adds its own IDesktop, IPdfPrinter, IMailer, IReceiptReader and IPlatformInfo
public static class UiServices
{
    public static IServiceCollection AddWattlebooksUi(this IServiceCollection services)
    {
        services.AddSingleton<StartupGate>();
        services.AddSingleton<DashboardFeed>();
        services.AddSingleton<PrefsStore>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<ShortcutService>();
        services.AddSingleton<DocumentPdfExporter>();
        services.AddSingleton<InvoicePdfExporter>();
        services.AddSingleton<StatementPdfExporter>();
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<EmailActions>();
        services.AddSingleton<LaunchRequests>();
        services.AddSingleton<RecurringRunner>();
        services.AddSingleton<SyncRunner>();
        return services;
    }
}
