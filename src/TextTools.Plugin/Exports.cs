using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TextTools.Core;

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
    private static bool _popupsCreated;

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
                "SQL Tools: copy a SQL statement from PL/SQL with its constants resolved.\r\n" +
                "Format Tools: formatting rules applied after the IDE Beautifier.\r\n" +
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

        // The saved formatting profile is what the menu command runs with.
        FormatSettingsStore.Apply(FormatSettingsStore.Load());

        // The menu is (re)built here because the IDE may activate the plug-in before asking for menu items;
        // the indices are the same either way.
        Menu.Build(Ide.SysVersion);

        // Everyday commands also go into the editor's context menu (same index: the click comes to OnMenuClick).
        // The IDE shows plug-in context items as a flat list - no submenus - so it is a short list (Commands.ContextMenu).
        // Once per process: OnActivate runs again when the plug-in is re-enabled, and the items must not double.
        var popups = _popupsCreated ? [] : Commands.ContextMenu;
        _popupsCreated = true;
        foreach (var name in popups)
        {
            if (Menu.IndexOf(name) is not { } index) continue;
            foreach (var window in (string[])["PROGRAMWINDOW", "SQLWINDOW", "TESTWINDOW"])
            {
                Ide.CreatePopupItem(_pluginId, index, Menu.DisplayName(index), window);
            }
        }

        // Ribbon (12+): images for the String Tools / Date Tools / SQL Tools buttons.
        if (Ide.SysVersion < 1200) return;
        foreach (var (index, name) in Menu.Buttons)
        {
            if (RibbonIcons.FileFor(name) is { } file) Ide.CreateToolButton(_pluginId, index, Menu.DisplayName(index), file);
        }
    });

    [UnmanagedCallersOnly(EntryPoint = "CreateMenuItem", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* CreateMenuItem(int index)
    {
        try
        {
            if (index == 1)
            {
                // The captions are UTF-8: ask the IDE for UTF-8 before the first (possibly translated) caption goes out.
                if (!Ide.Utf8) Ide.Utf8 = Ide.PlugInSetting(_pluginId, "CHARMODE", "UTF8");
                Menu.Build(Ide.SysVersion);
            }
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

    /// <summary>All plug-ins are loaded and the IDE has started: arm the self test when TEXTTOOLS_SELFTEST is set.</summary>
    [UnmanagedCallersOnly(EntryPoint = "AfterStart", CallConvs = [typeof(CallConvCdecl)])]
    public static void AfterStart() => Guard(SelfTest.StartIfRequested);

    /// <summary>Command Window: "TextTools selftest [result.json]".</summary>
    [UnmanagedCallersOnly(EntryPoint = "CommandLine", CallConvs = [typeof(CallConvCdecl)])]
    public static void CommandLine(int feedback, byte* command, byte* parameters) => Guard(() =>
    {
        var cmd = Marshal.PtrToStringUTF8((nint)command) ?? "";
        if (string.Equals(cmd.Trim(), "selftest", StringComparison.OrdinalIgnoreCase))
        {
            SelfTest.FromCommandWindow(feedback, Marshal.PtrToStringUTF8((nint)parameters) ?? "");
        }
        else if (string.Equals(cmd.Trim(), "format", StringComparison.OrdinalIgnoreCase))
        {
            BatchFormat.Run(feedback, Marshal.PtrToStringUTF8((nint)parameters) ?? "");
        }
        else
        {
            Ide.CommandFeedback(feedback, "Text Tools: unknown command; available: selftest [result.json], format <input> <beautifier output> <result>");
        }
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
