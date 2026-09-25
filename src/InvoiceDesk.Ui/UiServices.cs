// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Host;
using InvoiceDesk.Ui.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceDesk.Ui;

// each host adds its own IDesktop, IPdfPrinter, IMailer, IReceiptReader and IPlatformInfo
public static class UiServices
{
    public static IServiceCollection AddInvoiceDeskUi(this IServiceCollection services)
    {
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
