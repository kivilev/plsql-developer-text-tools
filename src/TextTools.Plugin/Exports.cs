using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TextTools.Plugin;

/// <summary>
/// Functions PL/SQL Developer looks up in the DLL (see plugindoc.pdf, "Basic functions" and "Event functions").
/// Crash safety rules for every export:
///  - no exception may leave it (an unhandled exception in an UnmanagedCallersOnly method kills the IDE process);
///  - the module is pinned on first call: a Native AOT runtime cannot be unloaded, so FreeLibrary must be a no-op;
///  - fallback return strings are static data, so the error path itself cannot fail.
/// </summary>
internal static unsafe partial class Exports
{
    private static int _pluginId;

    [UnmanagedCallersOnly(EntryPoint = "IdentifyPlugIn", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* IdentifyPlugIn(int id)
    {
        try
        {
            _pluginId = id;
            PinModule();
            return NativeStrings.Get(Menu.ProductName);
        }
        catch
        {
            return StaticString("Text Tools for PL/SQL Developer\0"u8);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "RegisterCallback", CallConvs = [typeof(CallConvCdecl)])]
    public static void RegisterCallback(int index, nint address)
    {
        try { Ide.Register(index, address); }
        catch { /* never let it reach the IDE */ }
    }

    [UnmanagedCallersOnly(EntryPoint = "PlugInName", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* PlugInName() => StaticString("TextTools\0"u8);

    [UnmanagedCallersOnly(EntryPoint = "About", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* About()
    {
        try
        {
            return NativeStrings.Get(
                $"{Menu.ProductName} {typeof(Exports).Assembly.GetName().Version?.ToString(3)}\r\n" +
                "String Tools: case, quoting, IN lists, encodings, lines.\r\n" +
                "Date Tools: now values, date and epoch conversion, date masks, intervals.\r\n" +
                "Author: Denis Kivilev");
        }
        catch
        {
            return StaticString("Text Tools for PL/SQL Developer\0"u8);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "OnActivate", CallConvs = [typeof(CallConvCdecl)])]
    public static void OnActivate() => Guard(() =>
    {
        // Without this the interface passes ANSI text and Cyrillic in the editor gets mangled.
        Ide.Utf8 = Ide.PlugInSetting(_pluginId, "CHARMODE", "UTF8");

        // Ribbon (12+): images for the String Tools / Date Tools buttons. The menu is rebuilt here because the IDE
        // may activate the plug-in before asking for menu items; the indices are the same either way.
        if (Ide.SysVersion < 1200) return;
        if (Menu.Buttons.Count == 0) Menu.Build(Ide.SysVersion);
        foreach (var (index, name) in Menu.Buttons)
        {
            if (RibbonIcons.FileFor(name) is { } file) Ide.CreateToolButton(_pluginId, index, name, file);
        }
    });

    [UnmanagedCallersOnly(EntryPoint = "CreateMenuItem", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* CreateMenuItem(int index)
    {
        try
        {
            if (index == 1) Menu.Build(Ide.SysVersion);
            return NativeStrings.Get(Menu.Caption(index));
        }
        catch
        {
            return StaticString("\0"u8);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "OnMenuClick", CallConvs = [typeof(CallConvCdecl)])]
    public static void OnMenuClick(int index) => Guard(() =>
    {
        if (Menu.Find(index) is { } cmd) Editor.Apply(cmd);
    });

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            try
            {
                MessageBoxW(Ide.AppHandle, $"{e.GetType().Name}: {e.Message}", Menu.ProductName, 0x10 /* MB_ICONERROR */);
            }
            catch
            {
                // Even reporting failed: swallow, the IDE must survive.
            }
        }
    }

    /// <summary>u8 literals live in the image's read-only data: the address is stable and nothing is allocated.</summary>
    private static byte* StaticString(ReadOnlySpan<byte> zeroTerminated) =>
        (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(zeroTerminated));

    /// <summary>
    /// Native AOT libraries cannot be unloaded: after FreeLibrary the runtime's own threads (GC, finalizer) would
    /// execute unmapped code and crash the IDE. Pinning makes later FreeLibrary calls no-ops.
    /// </summary>
    private static void PinModule()
    {
        const uint pin = 0x1, fromAddress = 0x4; // GET_MODULE_HANDLE_EX_FLAG_PIN | _FROM_ADDRESS
        GetModuleHandleExW(pin | fromAddress, (nint)(delegate* unmanaged[Cdecl]<int, byte*>)&IdentifyPlugIn, out _);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetModuleHandleExW(uint flags, nint address, out nint module);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
