using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TextTools.HostEmulator;

/// <summary>State and callbacks of a fake PL/SQL Developer editor window. Strings cross the boundary as UTF-8 char*.</summary>
internal static unsafe class FakeIde
{
    private static nint _textBuffer;
    private static nint _selectionBuffer;

    public static string Text { get; set; } = "";
    public static string Selected { get; set; } = "";
    public static string LastStatus { get; set; } = "";
    public static int X { get; set; }
    public static int Y { get; set; }
    public static bool AcceptUtf8 { get; set; } = true;
    public static List<(int Index, string Name, string File)> ToolButtons { get; } = [];

    /// <summary>A fake database: connected or not, every query returns <see cref="Rows"/> (one column) and is recorded.</summary>
    public static bool Connected { get; set; }
    public static IReadOnlyList<string> Rows { get; set; } = [];
    public static List<string> Queries { get; } = [];

    /// <summary>Context menu items the plugin created: (index, caption, window type).</summary>
    public static List<(int Index, string Name, string ObjectType)> PopupItems { get; } = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void PopupItem(int pluginId, int index, byte* name, byte* objectType) =>
        PopupItems.Add((index, Read(name), Read(objectType)));
    private static int _row;
    private static nint _fieldBuffer;

    public static void Register(delegate* unmanaged[Cdecl]<int, nint, void> register)
    {
        ToolButtons.Clear();
        register(1, (nint)(delegate* unmanaged[Cdecl]<int>)&SysVersion);
        register(11, (nint)(delegate* unmanaged[Cdecl]<int>)&IsConnected);
        register(40, (nint)(delegate* unmanaged[Cdecl]<byte*, int>)&SqlExecute);
        register(42, (nint)(delegate* unmanaged[Cdecl]<int>)&SqlEof);
        register(43, (nint)(delegate* unmanaged[Cdecl]<int>)&SqlNext);
        register(44, (nint)(delegate* unmanaged[Cdecl]<int, byte*>)&SqlField);
        register(84, (nint)(delegate* unmanaged[Cdecl]<int>)&WindowIsRunning);
        register(26, (nint)(delegate* unmanaged[Cdecl]<int>)&ReadOnly);
        register(30, (nint)(delegate* unmanaged[Cdecl]<byte*>)&GetText);
        register(31, (nint)(delegate* unmanaged[Cdecl]<byte*>)&GetSelected);
        register(34, (nint)(delegate* unmanaged[Cdecl]<byte*, int>)&SetText);
        register(35, (nint)(delegate* unmanaged[Cdecl]<byte*, int>)&Status);
        register(141, (nint)(delegate* unmanaged[Cdecl]<int>)&CursorX);
        register(142, (nint)(delegate* unmanaged[Cdecl]<int>)&CursorY);
        register(143, (nint)(delegate* unmanaged[Cdecl]<int, int, void>)&SetCursor);
        register(150, (nint)(delegate* unmanaged[Cdecl]<int, int, byte*, byte*, nint, void>)&ToolButton);
        register(153, (nint)(delegate* unmanaged[Cdecl]<int, int>)&HasEditor);
        register(219, (nint)(delegate* unmanaged[Cdecl]<int, byte*, byte*, int>)&Setting);
        register(69, (nint)(delegate* unmanaged[Cdecl]<int, int, byte*, byte*, void>)&PopupItem);
        register(20, (nint)(delegate* unmanaged[Cdecl]<int, byte*, int, void>)&CreateWindow);
        register(180, (nint)(delegate* unmanaged[Cdecl]<int, byte*, void>)&CommandFeedback);
        register(14, (nint)(delegate* unmanaged[Cdecl]<int>)&WindowType);
        register(48, (nint)(delegate* unmanaged[Cdecl]<byte*>)&SqlError);
        register(107, (nint)(delegate* unmanaged[Cdecl]<byte*, byte*, byte*, byte*, int>)&ShowHtml);
        register(108, (nint)(delegate* unmanaged[Cdecl]<byte*, byte*, int, int>)&RefreshHtml);
        register(12, (nint)(delegate* unmanaged[Cdecl]<byte**, byte**, byte**, void>)&MainConnectionInfo);
        register(39, (nint)(delegate* unmanaged[Cdecl]<int, int>)&Perform);
        register(50, (nint)(delegate* unmanaged[Cdecl]<int, int>)&UsePlugInSession);
        register(51, (nint)(delegate* unmanaged[Cdecl]<int, void>)&UseDefaultSession);
        register(57, (nint)(delegate* unmanaged[Cdecl]<int, byte*, byte*, byte*, byte*, int>)&SetPlugInSession);
        register(240, (nint)(delegate* unmanaged[Cdecl]<int, byte**, byte**, byte**, byte**, int>)&ConnectionInfoEx);
        register(245, (nint)(delegate* unmanaged[Cdecl]<int>)&GetWindowConnection);
        register(246, (nint)(delegate* unmanaged[Cdecl]<int, int>)&SetWindowConnection);
    }

    // The IDE's connections: 0 is the main one, 1 belongs to a window opened before the main connection was switched.
    private static readonly (nint User, nint Password, nint Database, nint ConnectAs)[] Connections =
    [
        (Marshal.StringToCoTaskMemUTF8("IY00444"), Marshal.StringToCoTaskMemUTF8("main-secret"), Marshal.StringToCoTaskMemUTF8("BANK"), Marshal.StringToCoTaskMemUTF8("")),
        (Marshal.StringToCoTaskMemUTF8("ACBS"), Marshal.StringToCoTaskMemUTF8("window-secret"), Marshal.StringToCoTaskMemUTF8("BANK"), Marshal.StringToCoTaskMemUTF8("")),
    ];

    /// <summary>The connection of the current window (IDE_GetWindowConnection) and whether a session for it can be opened.</summary>
    public static int WindowConnection { get; set; }
    public static bool SessionOpens { get; set; } = true;

    /// <summary>The session each query ran in ("main", or "user/password" of the plug-in session), and the order of the window calls.</summary>
    public static List<string> QuerySessions { get; } = [];
    public static List<string> Events { get; } = [];
    private static string _session = "main";
    private static string _pluginSession = "main";

    /// <summary>The session the next query would use: "main" once the plug-in has gone back to the default one.</summary>
    public static string CurrentSession => _session;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void MainConnectionInfo(byte** user, byte** password, byte** database)
    {
        *user = (byte*)Connections[0].User;
        *password = (byte*)Connections[0].Password;
        *database = (byte*)Connections[0].Database;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ConnectionInfoEx(int ix, byte** user, byte** password, byte** database, byte** connectAs)
    {
        if ((uint)ix >= (uint)Connections.Length) return 0;
        var c = Connections[ix];
        *user = (byte*)c.User;
        *password = (byte*)c.Password;
        *database = (byte*)c.Database;
        *connectAs = (byte*)c.ConnectAs;
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetWindowConnection() => WindowConnection;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetWindowConnection(int ix)
    {
        Events.Add($"set connection {ix}");
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Perform(int param)
    {
        Events.Add($"perform {param}");
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetPlugInSession(int pluginId, byte* user, byte* password, byte* database, byte* connectAs)
    {
        if (!SessionOpens) return 0;
        _pluginSession = $"{Read(user)}/{Read(password)}";
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int UsePlugInSession(int pluginId)
    {
        if (!SessionOpens) return 0;
        _session = _pluginSession;
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void UseDefaultSession(int pluginId) => _session = "main";

    /// <summary>The current window type (IDE_GetWindowType) and the HTML windows shown: (url, title, id).</summary>
    public static int CurrentWindowType { get; set; } = 1;
    public static List<(string Url, string Title, string Id)> HtmlWindows { get; } = [];
    private static nint _errorBuffer;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WindowType() => CurrentWindowType;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte* SqlError() => Keep(ref _errorBuffer, Connected ? "" : "ORA-03114: not connected to ORACLE");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ShowHtml(byte* url, byte* hash, byte* title, byte* id)
    {
        HtmlWindows.Add((Read(url), Read(title), Read(id)));
        CurrentWindowType = 7;
        return 1;
    }

    // Reloads the window with this ID if one was shown; the HTML window becomes current.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RefreshHtml(byte* url, byte* id, int bringToFront)
    {
        var key = Read(id);
        if (!HtmlWindows.Exists(w => string.Equals(w.Id, key, StringComparison.Ordinal))) return 0;
        CurrentWindowType = 7;
        return 1;
    }

    /// <summary>Windows the plugin created (type, text) and the lines it wrote to the Command Window.</summary>
    public static List<(int Type, string Text)> CreatedWindows { get; } = [];
    public static List<string> Feedback { get; } = [];

    // A new window becomes the current one: its text, no selection, the cursor at the start.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CreateWindow(int type, byte* text, int execute)
    {
        CreatedWindows.Add((type, Read(text)));
        Events.Add($"create window {type} execute {execute}");
        CurrentWindowType = type;
        (Text, Selected, X, Y) = (Read(text), "", 1, 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CommandFeedback(int handle, byte* text) => Feedback.Add(Read(text));

    private static byte* Keep(ref nint slot, string value)
    {
        if (slot != 0)
        {
            Marshal.FreeCoTaskMem(slot);
        }
        slot = Marshal.StringToCoTaskMemUTF8(value);
        return (byte*)slot;
    }

    private static string Read(byte* p) => Marshal.PtrToStringUTF8((nint)p) ?? "";

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsConnected() => Connected ? 1 : 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WindowIsRunning() => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SqlExecute(byte* sql)
    {
        Queries.Add(Read(sql));
        QuerySessions.Add(_session);
        _row = 0;
        return Connected ? 0 : 3114; // ORA-03114: not connected
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SqlEof() => _row >= Rows.Count ? 1 : 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SqlNext()
    {
        _row++;
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte* SqlField(int field) => Keep(ref _fieldBuffer, _row < Rows.Count ? Rows[_row] : "");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SysVersion() => 1600;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ReadOnly() => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte* GetText() => Keep(ref _textBuffer, Text);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte* GetSelected() => Keep(ref _selectionBuffer, Selected);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetText(byte* p)
    {
        Text = Read(p);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Status(byte* p)
    {
        LastStatus = Read(p);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CursorX() => X;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CursorY() => Y;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetCursor(int x, int y) => (X, Y) = (x, y);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HasEditor(int codeEditor) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ToolButton(int pluginId, int index, byte* name, byte* file, nint bitmap) => ToolButtons.Add((index, Read(name), Read(file)));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Setting(int pluginId, byte* name, byte* value) => AcceptUtf8 ? 1 : 0;
}
