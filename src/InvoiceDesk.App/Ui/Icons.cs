// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.App.Ui;

// 24px stroke icons kept inline so nothing loads from the network
public static class Icons
{
    static readonly Dictionary<string, string> Paths = new()
    {
        ["dashboard"] = """<rect x="3" y="3" width="7" height="9" rx="1.5"/><rect x="14" y="3" width="7" height="5" rx="1.5"/><rect x="14" y="12" width="7" height="9" rx="1.5"/><rect x="3" y="16" width="7" height="5" rx="1.5"/>""",
        ["invoice"] = """<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h6M9 9h2"/>""",
        ["users"] = """<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>""",
        ["wallet"] = """<path d="M19 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0 0 4h14a2 2 0 0 1 2 2v3"/><path d="M3 5v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-3"/><path d="M21 12h-4a2 2 0 0 0 0 4h4z"/>""",
        ["sliders"] = """<path d="M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6"/>""",
        ["search"] = """<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>""",
        ["plus"] = """<path d="M12 5v14M5 12h14"/>""",
        ["x"] = """<path d="M18 6 6 18M6 6l12 12"/>""",
        ["check"] = """<path d="M20 6 9 17l-5-5"/>""",
        ["chevron-down"] = """<path d="m6 9 6 6 6-6"/>""",
        ["chevron-left"] = """<path d="m15 6-6 6 6 6"/>""",
        ["download"] = """<path d="M12 3v12M7 10l5 5 5-5M5 21h14"/>""",
        ["send"] = """<path d="M22 2 11 13"/><path d="M22 2 15 22l-4-9-9-4z"/>""",
        ["copy"] = """<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>""",
        ["trash"] = """<path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>""",
        ["paperclip"] = """<path d="m21.4 11.1-9.2 9.2a6 6 0 0 1-8.5-8.5l9.2-9.2a4 4 0 0 1 5.7 5.7l-9.2 9.2a2 2 0 0 1-2.8-2.8l8.5-8.5"/>""",
        ["upload"] = """<path d="M12 21V9M7 14l5-5 5 5M5 3h14"/>""",
        ["external"] = """<path d="M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>""",
        ["folder"] = """<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>""",
        ["moon"] = """<path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/>""",
        ["sun"] = """<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>""",
        ["monitor"] = """<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>""",
        ["alert"] = """<circle cx="12" cy="12" r="9"/><path d="M12 8v5M12 16h.01"/>""",
        ["in"] = """<path d="M17 7 7 17M17 17H7V7"/>""",
        ["out"] = """<path d="M7 17 17 7M7 7h10v10"/>""",
        ["receipt"] = """<path d="M5 3v18l2-1.5L9 21l2-1.5 2 1.5 2-1.5 2 1.5 2-1.5V3l-2 1.5L15 3l-2 1.5L11 3 9 4.5 7 3z"/><path d="M9 9h6M9 13h6"/>""",
        ["edit"] = """<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z"/>""",
        ["ban"] = """<circle cx="12" cy="12" r="9"/><path d="m5.7 5.7 12.6 12.6"/>""",
        ["more"] = """<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>""",
        ["image"] = """<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-5-5L5 21"/>""",
        ["archive"] = """<rect x="2" y="3" width="20" height="5" rx="1"/><path d="M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8M10 12h4"/>""",
        ["undo"] = """<path d="M9 14 4 9l5-5"/><path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H11"/>""",
        ["hash"] = """<path d="M4 9h16M4 15h16M10 3 8 21M16 3l-2 18"/>""",
        ["database"] = """<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>""",
        ["mail"] = """<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>""",
        ["bell"] = """<path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.9 1.9 0 0 0 3.4 0"/>""",
        ["chart"] = """<path d="M3 3v18h18"/><path d="M7 15v2M11 11v6M15 7v10M19 12v5"/>""",
        ["refresh"] ="""<path d="M21 12a9 9 0 1 1-2.6-6.4L21 8"/><path d="M21 3v5h-5"/>""",
    };

    public static string Get(string name) => Paths.GetValueOrDefault(name, Paths["alert"]);
}
