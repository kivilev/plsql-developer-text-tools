using System.Runtime.InteropServices;

namespace TextTools.Plugin;

/// <summary>Puts Unicode text on the Windows clipboard. Returns false (never throws) when the clipboard is busy.</summary>
internal static unsafe partial class Clipboard
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const int Attempts = 10;

    /// <summary>The Unicode text on the clipboard, or null when it is busy or holds no text (self test).</summary>
    public static string? GetText(nint owner)
    {
        var opened = OpenClipboard(owner);
        for (var i = 1; i < Attempts && !opened; i++)
        {
            Thread.Sleep(20);
            opened = OpenClipboard(owner);
        }
        if (!opened) return null;
        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            var p = handle == 0 ? 0 : GlobalLock(handle);
            if (p == 0) return null;
            try { return Marshal.PtrToStringUni(p); }
            finally { GlobalUnlock(handle); }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetClipboardData(uint format);

    public static bool SetText(nint owner, string text)
    {
        // Another program may hold the clipboard for a moment (clipboard managers, RDP): retry briefly.
        var opened = OpenClipboard(owner);
        for (var i = 1; i < Attempts && !opened; i++)
        {
            Thread.Sleep(20);
            opened = OpenClipboard(owner);
        }
        if (!opened) return false;
        try
        {
            if (!EmptyClipboard()) return false;
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var memory = GlobalAlloc(GmemMoveable, bytes);
            if (memory == 0) return false;
            var target = GlobalLock(memory);
            if (target == 0)
            {
                GlobalFree(memory);
                return false;
            }
            fixed (char* source = text)
            {
                Buffer.MemoryCopy(source, (void*)target, (long)bytes, (long)bytes - sizeof(char));
            }
            ((char*)target)[text.Length] = '\0';
            GlobalUnlock(memory);
            // On success the clipboard owns the memory; on failure it is still ours.
            if (SetClipboardData(CfUnicodeText, memory) != 0) return true;
            GlobalFree(memory);
            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint SetClipboardData(uint format, nint memory);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint memory);
}
