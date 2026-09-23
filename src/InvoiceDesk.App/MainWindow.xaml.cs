// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using InvoiceDesk.App.Host;
using InvoiceDesk.Ui;
using InvoiceDesk.Ui.Host;
using InvoiceDesk.Core.Services;
using InvoiceDesk.Core.Storage;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace InvoiceDesk.App;

public partial class MainWindow : Window
{
    static readonly string[] EditingMenuItems = ["cut", "copy", "paste", "pasteAndMatchStyle", "selectAll", "undo", "redo", "spellCheck"];

    readonly AppPaths _paths;
    readonly PrefsStore _prefs;
    readonly ThemeService _theme;
    readonly ToastService _toasts;
    IntPtr _hwnd;
    Microsoft.Web.WebView2.Wpf.WebView2CompositionControl? _webView;
    readonly TaskbarBadge _badge;

    public MainWindow(IServiceProvider services)
    {
        _paths = services.GetRequiredService<AppPaths>();
        _prefs = services.GetRequiredService<PrefsStore>();
        _theme = services.GetRequiredService<ThemeService>();
        _toasts = services.GetRequiredService<ToastService>();

        Resources.Add("services", services);
        InitializeComponent();
        WindowPlacement.Restore(this, _prefs.Current);
        _badge = new TaskbarBadge(Taskbar, services.GetRequiredService<InvoiceService>(),
            services.GetRequiredService<TransactionService>(), Dispatcher);

        var dark = _theme.ResolveInitial();
        ApplyChrome(dark);
        WebView.BlazorWebViewInitializing += OnWebViewInitializing;
        WebView.BlazorWebViewInitialized += OnWebViewInitialized;
        _theme.ResolvedChanged += isDark => Dispatcher.BeginInvoke(() => ApplyChrome(isDark));
        _theme.TranslucentChanged += () => Dispatcher.BeginInvoke(() => ApplyChrome(_theme.IsDark));

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            services.GetRequiredService<HostWindow>().Handle = _hwnd;
            ApplyChrome(_theme.IsDark);
        };
        Closing += OnClosing;
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    void OnWebViewInitializing(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        e.UserDataFolder = _paths.WebView;
        // lets automated ui checks drive the app over the devtools protocol
        var port = Environment.GetEnvironmentVariable("INVOICEDESK_CDP_PORT");
        if (!string.IsNullOrWhiteSpace(port))
            e.EnvironmentOptions = new CoreWebView2EnvironmentOptions($"--remote-debugging-port={port}");
    }

    void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        var core = e.WebView.CoreWebView2;
        _webView = e.WebView;
        ApplyChrome(_theme.IsDark);
        VirtualHosts.Map(core, _paths);
        core.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        // browser keys and devtools would make this feel like a web page
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.ContextMenuRequested += (_, args) =>
        {
            var keep = args.MenuItems.Where(i => EditingMenuItems.Contains(i.Name)).ToList();
            args.MenuItems.Clear();
            foreach (var item in keep) args.MenuItems.Add(item);
            if (keep.Count == 0) args.Handled = true;
        };
    }

    // with mica every layer down to the window has to be see-through for it to show
    void ApplyChrome(bool dark)
    {
        var mica = _theme.Translucent;
        var desk = ThemeColours.Desk(dark);
        Background = mica ? Brushes.Transparent : new SolidColorBrush(desk);
        if (_webView is not null)
            _webView.DefaultBackgroundColor = mica ? System.Drawing.Color.Transparent : ThemeColours.DeskDrawing(dark);
        if (_hwnd == IntPtr.Zero) return;
        if (HwndSource.FromHwnd(_hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = mica ? Colors.Transparent : desk;
        WindowChrome.Apply(_hwnd, dark, desk, ThemeColours.Ink(dark), mica);
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        _badge.Dispose();
        WindowPlacement.Save(this, _prefs.Current);
        _prefs.Save();
        // pending undo-able deletes are committed rather than silently dropped
        Task.Run(_toasts.FlushAsync).Wait(TimeSpan.FromSeconds(5));
    }
}
