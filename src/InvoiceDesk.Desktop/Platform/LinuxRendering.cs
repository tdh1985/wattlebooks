// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace InvoiceDesk.Desktop.Platform;

// webkitgtk's dma-buf renderer paints a blank window without dri3, as in wsl, vms and nvidia
public static partial class LinuxRendering
{
    const string DmabufSwitch = "WEBKIT_DISABLE_DMABUF_RENDERER";

    public static IReadOnlyDictionary<string, string> EnvironmentFor(DialogOs os, Func<string, string?> current)
    {
        var set = new Dictionary<string, string>();
        if (os == DialogOs.Linux && current(DmabufSwitch) is null) set[DmabufSwitch] = "1";
        return set;
    }

    // must run before webkit starts, its web process reads these when it launches
    public static void Apply()
    {
        foreach (var (name, value) in EnvironmentFor(SystemDialog.CurrentOs, Environment.GetEnvironmentVariable))
            Set(name, value);
    }

    // .net keeps its own copy of the environment, native code like webkit only sees libc's
    public static void Set(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        if (!OperatingSystem.IsWindows()) setenv(name, value, 1);
    }

    public static string? NativeGet(string name) => Marshal.PtrToStringUTF8(getenv(name));

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int setenv(string name, string value, int overwrite);

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr getenv(string name);
}
