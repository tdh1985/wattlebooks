// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using InvoiceDesk.Core.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace InvoiceDesk.App.Host;

public enum MailOutcome
{
    // the email opened with the pdf already attached
    Attached,
    // the email opened but the pdf has to be dragged in by hand
    OpenedWithoutAttachment,
}

// hands a ready email to whatever mail app the pc uses, nothing is sent from here
public sealed class Mailer(Desktop desktop, ILogger<Mailer> log)
{
    // long mailto links get cut off by some mail apps and browsers
    const int MailtoBodyLimit = 1800;
    // a mail app that is going to fail usually says so quickly, one that works sits on its compose window
    static readonly TimeSpan MapiGrace = TimeSpan.FromSeconds(2.5);

    public async Task<MailOutcome> ComposeAsync(EmailDraft draft, string attachmentPath)
    {
        if (HasMapiClient())
        {
            var mapi = SendWithMapiAsync(draft, attachmentPath);
            var finished = await Task.WhenAny(mapi, Task.Delay(MapiGrace));
            // still running means its compose window is open and waiting on the user
            if (finished != mapi) return MailOutcome.Attached;
            var code = await mapi;
            if (code is SuccessSuccess or MapiUserAbort) return MailOutcome.Attached;
            log.LogWarning("Simple MAPI returned {Code}, falling back to mailto", code);
        }

        OpenMailto(draft);
        desktop.ShowInFolder(attachmentPath);
        return MailOutcome.OpenedWithoutAttachment;
    }

    static bool HasMapiClient()
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey(@"Software\Clients\Mail");
            if (key?.GetValue(null) is string name && name.Trim().Length > 0) return true;
        }
        return false;
    }

    static void OpenMailto(EmailDraft draft)
    {
        var body = draft.Body.Length > MailtoBodyLimit ? draft.Body[..MailtoBodyLimit] + "…" : draft.Body;
        var url = $"mailto:{Uri.EscapeDataString(draft.To)}?subject={Uri.EscapeDataString(draft.Subject)}" +
                  $"&body={Uri.EscapeDataString(body.Replace("\n", "\r\n"))}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // simple mapi blocks until the compose window closes, so it gets a thread of its own
    static Task<int> SendWithMapiAsync(EmailDraft draft, string attachmentPath)
    {
        var done = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { done.TrySetResult(SendWithMapi(draft, attachmentPath)); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
            {
                done.TrySetResult(MapiFailure);
            }
        })
        {
            IsBackground = true,
            Name = "mapi compose",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    static int SendWithMapi(EmailDraft draft, string attachmentPath)
    {
        var recipients = IntPtr.Zero;
        var files = IntPtr.Zero;
        try
        {
            var message = new MapiMessageW
            {
                Subject = draft.Subject,
                NoteText = draft.Body.Replace("\n", "\r\n"),
            };
            if (draft.To.Trim().Length > 0)
            {
                recipients = Marshal.AllocHGlobal(Marshal.SizeOf<MapiRecipDescW>());
                Marshal.StructureToPtr(new MapiRecipDescW
                {
                    RecipClass = MapiTo,
                    Name = draft.To.Trim(),
                    Address = "SMTP:" + draft.To.Trim(),
                }, recipients, false);
                message.RecipCount = 1;
                message.Recips = recipients;
            }

            files = Marshal.AllocHGlobal(Marshal.SizeOf<MapiFileDescW>());
            Marshal.StructureToPtr(new MapiFileDescW
            {
                Position = -1,
                PathName = attachmentPath,
                FileName = Path.GetFileName(attachmentPath),
            }, files, false);
            message.FileCount = 1;
            message.Files = files;

            return MAPISendMailW(IntPtr.Zero, IntPtr.Zero, message, MapiLogonUi | MapiDialog, 0);
        }
        finally
        {
            if (recipients != IntPtr.Zero)
            {
                Marshal.DestroyStructure<MapiRecipDescW>(recipients);
                Marshal.FreeHGlobal(recipients);
            }
            if (files != IntPtr.Zero)
            {
                Marshal.DestroyStructure<MapiFileDescW>(files);
                Marshal.FreeHGlobal(files);
            }
        }
    }

    const int SuccessSuccess = 0;
    const int MapiUserAbort = 1;
    const int MapiFailure = 2;
    const int MapiLogonUi = 0x1;
    const int MapiDialog = 0x8;
    const int MapiTo = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    sealed class MapiMessageW
    {
        public int Reserved;
        public string? Subject;
        public string? NoteText;
        public string? MessageType;
        public string? DateReceived;
        public string? ConversationId;
        public int Flags;
        public IntPtr Originator;
        public int RecipCount;
        public IntPtr Recips;
        public int FileCount;
        public IntPtr Files;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MapiRecipDescW
    {
        public int Reserved;
        public int RecipClass;
        public string? Name;
        public string? Address;
        public int EntryIdSize;
        public IntPtr EntryId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MapiFileDescW
    {
        public int Reserved;
        public int Flags;
        public int Position;
        public string? PathName;
        public string? FileName;
        public IntPtr FileType;
    }

    [DllImport("mapi32.dll", CharSet = CharSet.Unicode)]
    static extern int MAPISendMailW(IntPtr session, IntPtr uiParam, MapiMessageW message, int flags, int reserved);
}
