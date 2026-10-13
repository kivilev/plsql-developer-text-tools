using System.Runtime.InteropServices;

namespace TextTools.Plugin;

/// <summary>
/// PL/SQL Developer callbacks. Indices and signatures come from PlugInDoc\Interface\PlugInIntf.pas;
/// Bool and Integer are 32-bit, strings are single-byte char* (UTF-8 once CHARMODE=UTF8 is set).
/// </summary>
internal static unsafe class Ide
{
    private static readonly nint[] Callbacks = new nint[512];

    public static void Register(int index, nint address)
    {
        if ((uint)index < (uint)Callbacks.Length) Callbacks[index] = address;
    }

    private static nint Fn(int index) => Callbacks[index];
    private static bool Has(int index) => Callbacks[index] != 0;

    // 1
    public static int SysVersion => Has(1) ? ((delegate* unmanaged[Cdecl]<int>)Fn(1))() : 0;
    // 10
    public static void MenuState(int pluginId, int index, bool enabled)
    {
        if (Has(10)) ((delegate* unmanaged[Cdecl]<int, int, int, void>)Fn(10))(pluginId, index, enabled ? 1 : 0);
    }
    /// <summary>True once CHARMODE=UTF8 was accepted; until then text with non-ASCII characters must not be rewritten.</summary>
    public static bool Utf8 { get; set; }

    // 11
    public static bool Connected => Has(11) && ((delegate* unmanaged[Cdecl]<int>)Fn(11))() != 0;
    // 15 (an HWND; its significant bits fit into 32 bits on x64 too)
    public static nint AppHandle => Has(15) ? ((delegate* unmanaged[Cdecl]<int>)Fn(15))() : 0;
    // 84
    public static bool WindowIsRunning => Has(84) && ((delegate* unmanaged[Cdecl]<int>)Fn(84))() != 0;
    // 26
    public static bool GetReadOnly => Has(26) && ((delegate* unmanaged[Cdecl]<int>)Fn(26))() != 0;
    // 30
    public static string GetText() => Has(30) ? FromNative(((delegate* unmanaged[Cdecl]<byte*>)Fn(30))()) : "";
    // 31
    public static string GetSelectedText() => Has(31) ? FromNative(((delegate* unmanaged[Cdecl]<byte*>)Fn(31))()) : "";
    // 34
    public static bool SetText(string text) =>
        Has(34) && WithNative(text, p => ((delegate* unmanaged[Cdecl]<byte*, int>)Fn(34))(p) != 0);
    // 35
    public static void SetStatusMessage(string text)
    {
        if (Has(35)) WithNative(text, p => ((delegate* unmanaged[Cdecl]<byte*, int>)Fn(35))(p));
    }
    // 40, 42, 44: runs in the main IDE session; field indices are 0-based, the first row is current right after execute.
    public static string? QuerySingleValue(string sql)
    {
        if (!Has(40) || !Has(42) || !Has(44)) return null;
        var rc = WithNative(sql, p => ((delegate* unmanaged[Cdecl]<byte*, int>)Fn(40))(p));
        if (rc != 0 || ((delegate* unmanaged[Cdecl]<int>)Fn(42))() != 0) return null;
        return FromNative(((delegate* unmanaged[Cdecl]<int, byte*>)Fn(44))(0));
    }
    // 141, 142, 143
    public static int GetCursorX() => Has(141) ? ((delegate* unmanaged[Cdecl]<int>)Fn(141))() : 0;
    public static int GetCursorY() => Has(142) ? ((delegate* unmanaged[Cdecl]<int>)Fn(142))() : 0;
    public static void SetCursor(int x, int y)
    {
        if (Has(143)) ((delegate* unmanaged[Cdecl]<int, int, void>)Fn(143))(x, y);
    }
    // 153
    public static bool WindowHasEditor(bool codeEditor) =>
        Has(153) && ((delegate* unmanaged[Cdecl]<int, int>)Fn(153))(codeEditor ? 1 : 0) != 0;
    // 219
    public static bool PlugInSetting(int pluginId, string setting, string value) =>
        Has(219) && WithNative(setting, s => WithNative(value, v =>
            ((delegate* unmanaged[Cdecl]<int, byte*, byte*, int>)Fn(219))(pluginId, s, v) != 0));

    private static string FromNative(byte* p) => p == null ? "" : Marshal.PtrToStringUTF8((nint)p) ?? "";

    private delegate T NativeFunc<out T>(byte* p);

    private static T WithNative<T>(string s, NativeFunc<T> f)
    {
        var p = (byte*)Marshal.StringToCoTaskMemUTF8(s);
        try { return f(p); }
        finally { Marshal.FreeCoTaskMem((nint)p); }
    }
}
