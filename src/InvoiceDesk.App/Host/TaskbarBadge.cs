// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using InvoiceDesk.Core.Services;

using InvoiceDesk.Ui.Host;

namespace InvoiceDesk.App.Host;

// the overdue count on the taskbar icon, so late invoices are noticed without opening the app
public sealed class TaskbarBadge : IDisposable
{
    readonly System.Windows.Shell.TaskbarItemInfo _taskbar;
    readonly InvoiceService _invoices;
    readonly TransactionService _ledger;
    readonly Dispatcher _dispatcher;
    // invoices tip into overdue at midnight without anything being saved
    readonly DispatcherTimer _hourly;
    int _shown = -1;

    public TaskbarBadge(System.Windows.Shell.TaskbarItemInfo taskbar, InvoiceService invoices, TransactionService ledger, Dispatcher dispatcher)
    {
        _taskbar = taskbar;
        _invoices = invoices;
        _ledger = ledger;
        _dispatcher = dispatcher;
        _invoices.Changed += Refresh;
        _ledger.Changed += Refresh;
        _hourly = new DispatcherTimer(TimeSpan.FromHours(1), DispatcherPriority.Background, (_, _) => Refresh(), dispatcher);
        _hourly.Start();
        Refresh();
    }

    public void Refresh() => _dispatcher.BeginInvoke(async () =>
    {
        try
        {
            var count = (await _invoices.ListAsync(InvoiceFilter.Overdue)).Count;
            if (count == _shown) return;
            _shown = count;
            _taskbar.Overlay = count == 0 ? null : Render(count);
            _taskbar.Description = count switch
            {
                0 => "",
                1 => "1 overdue invoice",
                _ => $"{count} overdue invoices",
            };
        }
        catch (Exception ex)
        {
            FileLog.Write(ex, "taskbar badge");
        }
    });

    static ImageSource Render(int count)
    {
        const double size = 32;
        var text = count > 9 ? "9+" : count.ToString(CultureInfo.InvariantCulture);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C)), new Pen(Brushes.White, 2),
                new Point(size / 2, size / 2), size / 2 - 1, size / 2 - 1);
            var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                text.Length > 1 ? 15 : 19, Brushes.White, 1.0);
            dc.DrawText(label, new Point((size - label.Width) / 2, (size - label.Height) / 2));
        }
        var bitmap = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose()
    {
        _hourly.Stop();
        _invoices.Changed -= Refresh;
        _ledger.Changed -= Refresh;
    }
}
