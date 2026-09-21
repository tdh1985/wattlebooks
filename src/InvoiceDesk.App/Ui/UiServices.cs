// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.App.Host;
using InvoiceDesk.App.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceDesk.App.Ui;

public static class UiServices
{
    public static IServiceCollection AddInvoiceDeskUi(this IServiceCollection services)
    {
        services.AddSingleton<PrefsStore>();
        services.AddSingleton<Desktop>();
        services.AddSingleton<HostWindow>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<ShortcutService>();
        services.AddSingleton<PdfPrinter>();
        services.AddSingleton<DocumentPdfExporter>();
        services.AddSingleton<InvoicePdfExporter>();
        services.AddSingleton<StatementPdfExporter>();
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<Mailer>();
        services.AddSingleton<EmailActions>();
        services.AddSingleton<LaunchRequests>();
        services.AddSingleton<ReceiptReader>();
        services.AddSingleton<RecurringRunner>();
        return services;
    }
}
