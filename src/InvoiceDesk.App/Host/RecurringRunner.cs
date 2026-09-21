// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Windows.Threading;
using InvoiceDesk.App.Ui;
using InvoiceDesk.Core.Services;

namespace InvoiceDesk.App.Host;

// makes repeating drafts when the app opens and each hour, since there's no background service
public sealed class RecurringRunner(RecurringService recurring, ToastService toasts)
{
    DispatcherTimer? _hourly;
    int _madeAtStartup;

    // one series can catch up several dates, so this counts drafts
    public static string Message(int made) => made == 1
        ? "A new repeating draft is ready to check and send."
        : $"{made} new repeating drafts are ready to check and send.";

    public async Task RunAtStartupAsync() => _madeAtStartup = await RunAsync();

    // the ui isn't up at startup, so the layout asks for this once it has rendered
    public int TakeStartupCount() => Interlocked.Exchange(ref _madeAtStartup, 0);

    public void StartHourly(Dispatcher dispatcher)
    {
        _hourly = new DispatcherTimer(TimeSpan.FromHours(1), DispatcherPriority.Background, async (_, _) =>
        {
            var made = await RunAsync();
            if (made > 0) toasts.Show(Message(made), ToastKind.Info);
        }, dispatcher);
        _hourly.Start();
    }

    async Task<int> RunAsync()
    {
        try { return await recurring.GenerateDueAsync(); }
        catch (Exception ex)
        {
            FileLog.Write(ex, "repeating invoices");
            return 0;
        }
    }
}
