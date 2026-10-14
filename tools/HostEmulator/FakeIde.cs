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

    public static void Register(delegate* unmanaged[Cdecl]<int, nint, void> register)
    {
        ToolButtons.Clear();
        register(1, (nint)(delegate* unmanaged[Cdecl]<int>)&SysVersion);
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
    }

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
