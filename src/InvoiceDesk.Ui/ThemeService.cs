// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Ui.Host;
using InvoiceDesk.Ui.Platform;

namespace InvoiceDesk.Ui;

public sealed class ThemeService(PrefsStore prefs, IPlatformInfo platform)
{
    public static readonly string[] Choices = ["system", "light", "dark"];

    public string Label(string choice) => choice switch { "light" => "Light", "dark" => "Dark", _ => $"Match {platform.SystemName}" };

    public string Theme => prefs.Current.Theme;
    public bool IsDark { get; private set; }

    public event Action? Changed;
    public event Action<bool>? ResolvedChanged;
    public event Action? TranslucentChanged;

    // mica only exists on windows 11 22h2 and later, older builds keep the solid desk
    public bool CanBeTranslucent => platform.CanBeTranslucent;

    public bool Translucent => CanBeTranslucent && prefs.Current.Translucent;

    public void SetTranslucent(bool on)
    {
        if (prefs.Current.Translucent == on) return;
        prefs.Current.Translucent = on;
        prefs.Save();
        TranslucentChanged?.Invoke();
    }

    public void Set(string theme)
    {
        if (!Choices.Contains(theme) || theme == Theme) return;
        prefs.Current.Theme = theme;
        prefs.Save();
        Changed?.Invoke();
    }

    // the page reports what it actually painted, which settles "system"
    public void ReportResolved(bool dark)
    {
        if (dark == IsDark) return;
        IsDark = dark;
        ResolvedChanged?.Invoke(dark);
    }

    public bool ResolveInitial()
    {
        IsDark = Theme switch
        {
            "dark" => true,
            "light" => false,
            _ => platform.SystemPrefersDark(),
        };
        return IsDark;
    }
}
