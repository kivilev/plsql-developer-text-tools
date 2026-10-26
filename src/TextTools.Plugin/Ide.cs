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
    // 130, 131: the translation file and its language the IDE uses (Preferences: Language); empty when none is used (English).
    public static string TranslationFile => Has(130) ? FromNative(((delegate* unmanaged[Cdecl]<byte*>)Fn(130))()) : "";
    public static string TranslationLanguage => Has(131) ? FromNative(((delegate* unmanaged[Cdecl]<byte*>)Fn(131))()) : "";
    // 14: IDE_GetWindowType - the current window: 1 SQL, 2 Test, 3 Program, 4 Command, 5 Plan, 6 Report, 7 HTML.
    public static int GetWindowType() => Has(14) ? ((delegate* unmanaged[Cdecl]<int>)Fn(14))() : 0;
    // 48: SQL_ErrorMessage - the last error of SQL_Execute (e.g. "ORA-00942: table or view does not exist").
    public static string SqlErrorMessage() => Has(48) ? FromNative(((delegate* unmanaged[Cdecl]<byte*>)Fn(48))()) : "";
    // 107: IDE_ShowHTML(Url, Hash, Title, ID) - an HTML file in a child window; ID lets IDE_RefreshHTML find it again.
    public static bool ShowHtml(string url, string hash, string title, string id) =>
        Has(107) && WithNative(url, u => WithNative(hash, h => WithNative(title, t => WithNative(id, i =>
            ((delegate* unmanaged[Cdecl]<byte*, byte*, byte*, byte*, int>)Fn(107))(u, h, t, i) != 0))));
    // 108: IDE_RefreshHTML(Url, ID, BringToFront) - reloads the window with this ID; false when there is none.
    public static bool RefreshHtml(string url, string id, bool bringToFront) =>
        Has(108) && WithNative(url, u => WithNative(id, i =>
            ((delegate* unmanaged[Cdecl]<byte*, byte*, int, int>)Fn(108))(u, i, bringToFront ? 1 : 0) != 0));
    // 20: IDE_CreateWindow(WindowType, Text, Execute) - 1 = SQL Window, 5 = Plan Window; the new window becomes current.
    public static void CreateWindow(int windowType, string text, bool execute = false)
    {
        if (!Has(20)) return;
        WithNative(text, t =>
        {
            ((delegate* unmanaged[Cdecl]<int, byte*, int, void>)Fn(20))(windowType, t, execute ? 1 : 0);
            return 0;
        });
    }
    // 180: IDE_CommandFeedback(FeedbackHandle, S) - a line of output in the Command Window that ran CommandLine.
    public static void CommandFeedback(int feedback, string text)
    {
        if (!Has(180)) return;
        WithNative(text, t =>
        {
            ((delegate* unmanaged[Cdecl]<int, byte*, void>)Fn(180))(feedback, t);
            return 0;
        });
    }
    // 69: IDE_CreatePopupItem(ID, Index, Name, ObjectType) - a context menu item; a click comes to OnMenuClick(Index).
    // ObjectType PROGRAMWINDOW / SQLWINDOW / TESTWINDOW / COMMANDWINDOW = the editor of that window type.
    public static void CreatePopupItem(int pluginId, int index, string name, string objectType)
    {
        if (!Has(69)) return;
        WithNative(name, n => WithNative(objectType, t =>
        {
            ((delegate* unmanaged[Cdecl]<int, int, byte*, byte*, void>)Fn(69))(pluginId, index, n, t);
            return 0;
        }));
    }
    // 110: IDE_GetWindowObject(var ObjectType, ObjectOwner, ObjectName, SubObject: PAnsiChar): Bool - the object
    // edited in the current window (a Program Window); null for SQL windows and files.
    public static (string Type, string Owner, string Name)? GetWindowObject()
    {
        if (!Has(110)) return null;
        byte* type = null, owner = null, name = null, sub = null;
        if (((delegate* unmanaged[Cdecl]<byte**, byte**, byte**, byte**, int>)Fn(110))(&type, &owner, &name, &sub) == 0) return null;
        var o = FromNative(owner);
        var n = FromNative(name);
        return o.Length > 0 && n.Length > 0 ? (FromNative(type), o, n) : null;
    }
    // 40, 42, 43, 44: the first column of every row (up to maxRows), or null when the query failed.
    public static List<string>? QueryColumn(string sql, int maxRows)
    {
        if (!Has(40) || !Has(42) || !Has(43) || !Has(44)) return null;
        var rc = WithNative(sql, p => ((delegate* unmanaged[Cdecl]<byte*, int>)Fn(40))(p));
        if (rc != 0) return null;
        var rows = new List<string>();
        while (rows.Count < maxRows && ((delegate* unmanaged[Cdecl]<int>)Fn(42))() == 0)
        {
            rows.Add(FromNative(((delegate* unmanaged[Cdecl]<int, byte*>)Fn(44))(0)));
            ((delegate* unmanaged[Cdecl]<int>)Fn(43))();
        }
        return rows;
    }
    // 141, 142, 143
    public static int GetCursorX() => Has(141) ? ((delegate* unmanaged[Cdecl]<int>)Fn(141))() : 0;
    public static int GetCursorY() => Has(142) ? ((delegate* unmanaged[Cdecl]<int>)Fn(142))() : 0;
    public static void SetCursor(int x, int y)
    {
        if (Has(143)) ((delegate* unmanaged[Cdecl]<int, int, void>)Fn(143))(x, y);
    }
    // 153
    /// <summary>150: the image of a ribbon button (index = its CreateMenuItem index); a .bmp/.png file keeps transparency.</summary>
    public static void CreateToolButton(int pluginId, int index, string name, string bitmapFile)
    {
        if (!Has(150)) return;
        WithNative(name, n => WithNative(bitmapFile, f =>
        {
            ((delegate* unmanaged[Cdecl]<int, int, byte*, byte*, nint, void>)Fn(150))(pluginId, index, n, f, 0);
            return 0;
        }));
    }

    public static bool WindowHasEditor(bool codeEditor) =>
        Has(153) && ((delegate* unmanaged[Cdecl]<int, int>)Fn(153))(codeEditor ? 1 : 0) != 0;
    // 219
    public static bool PlugInSetting(int pluginId, string setting, string value) =>
        Has(219) && WithNative(setting, s => WithNative(value, v =>
            ((delegate* unmanaged[Cdecl]<int, byte*, byte*, int>)Fn(219))(pluginId, s, v) != 0));

    // 162: IDE_BeautifyText(S) - the text formatted by the Beautifier with the rules that are set in the IDE; null when it cannot.
    public static string? BeautifyText(string text)
    {
        if (!Has(162)) return null;
        return WithNative(text, p =>
        {
            var result = ((delegate* unmanaged[Cdecl]<byte*, byte*>)Fn(162))(p);
            return result == null ? null : FromNative(result);
        });
    }

    private static string FromNative(byte* p) => p == null ? "" : Marshal.PtrToStringUTF8((nint)p) ?? "";

    private delegate T NativeFunc<out T>(byte* p);

    private static T WithNative<T>(string s, NativeFunc<T> f)
    {
        var p = (byte*)Marshal.StringToCoTaskMemUTF8(s);
        try { return f(p); }
        finally { Marshal.FreeCoTaskMem((nint)p); }
    }
}
