// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Globalization;
using InvoiceDesk.Core;
using InvoiceDesk.Core.Data;
using InvoiceDesk.Core.Rules;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using InvoiceDesk.Desktop.Platform;
using InvoiceDesk.Ui;
using InvoiceDesk.Ui.Host;
using InvoiceDesk.Ui.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;

namespace InvoiceDesk.Desktop;

// the mac and linux host, the same startup steps as the windows app.xaml.cs
static class Program
{
    const string Title = "InvoiceDesk";
    const string FilesScheme = "idfiles";
    const string LocalScheme = "idlocal";

    [STAThread]
    static int Main(string[] args)
    {
        var paths = AppPaths.Default();
        // a synced folder can vanish briefly, such as before a sync app signs in
        while (paths.IsCustomLocation && !DataLocation.HasData(paths.DataRoot))
        {
            var choice = SystemDialog.Ask(Title,
                $"InvoiceDesk keeps your data in:\n{paths.DataRoot}\n\nThat folder or its database can't be found right now. If it's in a synced folder, check that its app is running and has finished syncing.\n\nYes: try again\nNo: switch back to this computer's own data\nCancel: close InvoiceDesk",
                DialogButtons.YesNoCancel);
            if (choice == DialogChoice.No)
            {
                DataLocation.Save(paths.LocalRoot, null);
                paths = AppPaths.Default();
            }
            else if (choice != DialogChoice.Yes)
            {
                return 0;
            }
        }

        var route = LaunchArgs.RouteFor(args);
        using var instance = new SingleInstance(paths.DataRoot, paths.LocalRoot);
        if (!instance.IsFirst)
        {
            instance.SignalFirst(route);
            return 0;
        }

        paths.EnsureCreated();
        FileLog.Initialize(paths.Logs);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => FileLog.Write(e.ExceptionObject as Exception, "unhandled");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            FileLog.Write(e.Exception, "unobserved task");
            e.SetObserved();
        };

        var dataLock = DataLock.ForThisProcess(paths, TimeProvider.System);
        if (dataLock.ReadOther() is { } other && !ConfirmOpenElsewhere(other)) return 0;
        dataLock.Acquire();
        using var heartbeat = new Timer(_ => dataLock.Heartbeat(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        try
        {
            return Run(paths, route, instance);
        }
        catch (Exception ex) when (MissingWebView.IsCause(ex))
        {
            FileLog.Write(ex, "webview");
            SystemDialog.Ask(Title, MissingWebView.Message(SystemDialog.CurrentOs), DialogButtons.Ok);
            return 1;
        }
        finally
        {
            // closed files let a sync app upload a complete database
            SqliteConnection.ClearAllPools();
            dataLock.Release();
        }
    }

    static int Run(AppPaths paths, string? route, SingleInstance instance)
    {
        var window = new PhotinoWindowRef();
        var builder = PhotinoBlazorAppBuilder.CreateDefault(EmbeddedAssets.Instance, []);
        builder.Services.AddLogging(logging => logging.AddProvider(new FileLogProvider()));
        // a brand-new data folder starts in the country the computer is set to
        builder.Services.AddInvoiceDeskCore(paths, RegionInfo.CurrentRegion.TwoLetterISORegionName);
        builder.Services.AddInvoiceDeskUi();
        builder.Services.AddSingleton(window);
        builder.Services.AddSingleton<IPlatformInfo, DesktopPlatform>();
        builder.Services.AddSingleton<IDesktop>(new UnixDesktop(window));
        builder.Services.AddSingleton<IPdfPrinter>(sp => new ChromePdfPrinter(paths, sp.GetRequiredService<IDesktop>()));
        builder.Services.AddSingleton<IMailer, MailtoMailer>();
        builder.Services.AddSingleton<IReceiptReader, NoReceiptReader>();
        builder.RootComponents.Add<Main>("#app");
        var app = builder.Build();
        window.Window = app.MainWindow;
        var services = app.Services;

        try
        {
            services.GetRequiredService<DatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
            var profiles = services.GetRequiredService<ProfileService>();
            Format.UseCountry(profiles.GetAsync().GetAwaiter().GetResult().Country);
            // the pdf renderer and every page read the country through format
            profiles.Changed += async () =>
            {
                try { Format.UseCountry((await profiles.GetAsync()).Country); }
                catch (Exception ex) { FileLog.Write(ex, "country change"); }
            };
            services.GetRequiredService<RecurringRunner>().RunAtStartupAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            FileLog.Write(ex, "startup");
            SystemDialog.Ask(Title, $"InvoiceDesk couldn't open its data folder.\n\n{ex.Message}\n\nDetails were saved in {paths.Logs}.", DialogButtons.Ok);
            return 1;
        }

        var prefs = services.GetRequiredService<PrefsStore>();
        var toasts = services.GetRequiredService<ToastService>();
        var launches = services.GetRequiredService<LaunchRequests>();
        var recurring = services.GetRequiredService<RecurringRunner>();

        // receipts and logos load through schemes of our own, like webview2's virtual hosts on windows
        FilesUrl.Configure($"{FilesScheme}://data/", $"{LocalScheme}://data/");
        app.MainWindow.RegisterCustomSchemeHandler(FilesScheme, (object _, string _, string url, out string type) => FileScheme.Serve(paths.DataRoot, url, out type)!);
        app.MainWindow.RegisterCustomSchemeHandler(LocalScheme, (object _, string _, string url, out string type) => FileScheme.Serve(paths.LocalRoot, url, out type)!);

        app.MainWindow
            .SetTitle(Title)
            .SetIconFile(IconFile(paths))
            .SetUseOsDefaultLocation(true)
            .SetMinSize(960, 640)
            .SetWidth((int)(prefs.Current.Width ?? 1360))
            .SetHeight((int)(prefs.Current.Height ?? 860))
            .SetMaximized(prefs.Current.Maximized);
#if !DEBUG
        // browser keys and devtools would make this feel like a web page
        app.MainWindow.SetDevToolsEnabled(false);
#endif
        // lets automated ui checks drive the app over the devtools protocol, webview2 only
        var port = Environment.GetEnvironmentVariable("INVOICEDESK_CDP_PORT");
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(port))
            app.MainWindow.SetBrowserControlInitParameters($"--remote-debugging-port={port}");

        app.MainWindow.RegisterWindowClosingHandler((_, _) =>
        {
            SavePlacement(app.MainWindow, prefs.Current);
            prefs.Save();
            // pending undo-able deletes are committed rather than silently dropped
            Task.Run(toasts.FlushAsync).Wait(TimeSpan.FromSeconds(5));
            return false;
        });

        launches.Request(route);
        instance.ListenForActivation(next => app.MainWindow.Invoke(() =>
        {
            app.MainWindow.SetMinimized(false);
            app.MainWindow.SetTopMost(true);
            app.MainWindow.SetTopMost(false);
            launches.Request(next);
        }));
        recurring.StartHourly();

        app.Run();

        recurring.Stop();
        try { (services as IDisposable)?.Dispose(); }
        catch (Exception ex) { FileLog.Write(ex, "shutdown"); }
        return 0;
    }

    // the position is left to the os so a missing monitor can't hide the window
    static void SavePlacement(Photino.NET.PhotinoWindow window, AppPrefs prefs)
    {
        prefs.Maximized = window.Maximized;
        if (window.Maximized || window.Minimized) return;
        prefs.Width = window.Width;
        prefs.Height = window.Height;
    }

    // photino wants the icon as a file on disk
    static string IconFile(AppPaths paths)
    {
        var file = Path.Combine(paths.LocalRoot, "icon.png");
        try
        {
            using var source = typeof(Program).Assembly.GetManifestResourceStream("icon.png")!;
            using var target = File.Create(file);
            source.CopyTo(target);
        }
        catch (IOException ex) { FileLog.Write(ex, "window icon"); }
        return file;
    }

    static bool ConfirmOpenElsewhere(LockInfo other)
    {
        var minutes = Math.Max(1, (int)Math.Round((DateTimeOffset.UtcNow - other.Heartbeat).TotalMinutes));
        var answer = SystemDialog.Ask(Title,
            $"InvoiceDesk looks like it's open on {other.Machine} (active {minutes} min ago).\n\nUsing the same data on two computers at once can lose changes. If you can, close it on {other.Machine} first.\n\nOpen it here anyway?",
            DialogButtons.YesNo);
        return answer == DialogChoice.Yes;
    }
}
