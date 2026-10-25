using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TextTools.Core;
using TextTools.Core.Formatting;

namespace TextTools.Plugin;

/// <summary>
/// The settings window of the formatting rules, drawn with plain Win32 (a Native AOT DLL has no WinForms), laid out like the
/// IDE's own Beautifier Rules window: tabs and the settings of the tab on top, a large preview below (the sample before and after
/// the rules, following every click), Save / Default / Close at the bottom. The window can be resized: the preview takes the room.
/// It is modal over the IDE and runs its own message loop. All texts come from <see cref="UiTexts"/> in the language of the IDE;
/// nothing here throws into the IDE.
/// </summary>
internal static unsafe partial class SettingsWindow
{
    private const string ClassName = "TextToolsFormatSettings";

    private const int IdSave = 1;
    private const int IdClose = 2;
    private const int IdDefault = 3;
    private const int IdTab = 100;
    private const int IdSetting = 200;
    private const int IdPreview = 400;

    // Window styles and messages (winuser.h).
    private const uint WsChild = 0x40000000, WsVisible = 0x10000000, WsTabStop = 0x10000, WsCaption = 0x00C00000, WsSysMenu = 0x80000;
    private const uint WsClipSiblings = 0x04000000, WsClipChildren = 0x02000000, WsVScroll = 0x200000, WsHScroll = 0x100000, WsBorder = 0x800000;
    private const uint WsThickFrame = 0x40000, WsMaximizeBox = 0x10000, WsExDlgModalFrame = 1;
    private const uint BsPushButton = 0, BsDefPushButton = 1, BsAutoCheckBox = 3;
    private const uint EsNumber = 0x2000;
    private const uint WmDestroy = 0x2, WmSize = 0x5, WmClose = 0x10, WmGetMinMaxInfo = 0x24, WmSetFont = 0x30, WmCommand = 0x111, WmNotify = 0x4E;
    private const uint BmGetCheck = 0xF0, BmSetCheck = 0xF1, TcmGetCurSel = 0x130B, TcmInsertItemW = 0x133E;
    private const uint WmCtlColorStatic = 0x138;
    private const int TcnSelChange = -551, EnChange = 0x300, SwHide = 0, SwShow = 5, SizeMinimized = 1;

    // Layout in pixels at 96 dpi (scaled by S).
    private const int StartWidth = 1000;
    private const int StartHeight = 720;
    private const int MinWidth = 720;
    private const int MinHeight = 520;
    private const int Gap = 12;
    private const int RowHeight = 28;
    private const int TabHeaderHeight = 28;
    private const int ButtonHeight = 28;
    private const int ButtonWidth = 110;
    private const int MinColumnWidth = 480;
    private static int _shownTab;

    private static nint _hwnd, _tab, _view, _save, _default, _close, _uiFont;
    private static int _dpi = 96;
    private static bool _updating;
    private static UiTexts _texts = UiTexts.English;
    private static FormatProfile _profile = new();

    /// <summary>A control of a setting: on which tab, in which row, and whether it is a checkbox (or a label and a number box).</summary>
    private sealed record Placed(int Tab, int Row, nint Window, bool IsLabel);

    private static readonly List<Placed> Placements = [];
    private static readonly Dictionary<string, nint> SettingControls = new(StringComparer.Ordinal);

    /// <summary>Shows the window over <paramref name="owner"/> and returns when it is closed.</summary>
    public static void Show(nint owner)
    {
        // The host emulator clicks every menu item of the plug-in in modes that have no one to close a modal window.
        if (string.Equals(Environment.GetEnvironmentVariable("TEXTTOOLS_NO_WINDOWS"), "1", StringComparison.Ordinal)) return;
        if (_hwnd != 0)
        {
            SetForegroundWindow(_hwnd);
            return;
        }
        _texts = UiTexts.For(Ide.TranslationLanguage, Ide.TranslationFile);
        _profile = FormatSettingsStore.Load();
        _dpi = Dpi();
        Placements.Clear();
        SettingControls.Clear();

        var icc = new InitCommonControls { Size = (uint)sizeof(InitCommonControls), Classes = 0x8 }; // ICC_TAB_CLASSES
        InitCommonControlsEx(in icc);
        if (!Register() || !RegisterCodeView()) return;

        var width = Math.Min(S(StartWidth) + FrameWidth(), GetSystemMetrics(0) - 40);
        var height = Math.Min(S(StartHeight) + FrameHeight(), GetSystemMetrics(1) - 80);
        var (x, y) = Center(owner, width, height);

        _hwnd = CreateWindowExW(WsExDlgModalFrame, ClassName, _texts.Text("settings.title"), Style, x, y, width, height, owner, 0, 0, 0);
        if (_hwnd == 0) return;
        try
        {
            _uiFont = CreateFontW(-MulDiv(9, _dpi, 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
            BuildControls();
            PushProfileToControls();
            Layout();
            UpdatePreview();
            if (owner != 0) EnableWindow(owner, 0);
            ShowWindow(_hwnd, SwShow);
            UpdateWindow(_hwnd);
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                if (IsDialogMessageW(_hwnd, ref msg) == 0)
                {
                    TranslateMessage(in msg);
                    DispatchMessageW(in msg);
                }
            }
        }
        finally
        {
            if (owner != 0)
            {
                EnableWindow(owner, 1);
                SetForegroundWindow(owner);
            }
            _hwnd = 0;
            if (_uiFont != 0) DeleteObject(_uiFont);
            _uiFont = 0;
        }
    }

    private static uint Style => WsCaption | WsSysMenu | WsThickFrame | WsMaximizeBox | WsClipChildren;

    private static int FrameWidth() => FrameSize().Width;

    private static int FrameHeight() => FrameSize().Height;

    /// <summary>What the title bar and the borders add to a client area.</summary>
    private static (int Width, int Height) FrameSize()
    {
        var r = new Rect();
        AdjustWindowRectEx(ref r, Style, 0, WsExDlgModalFrame);
        return (r.Right - r.Left, r.Bottom - r.Top);
    }

    private static void BuildControls()
    {
        _tab = Child("SysTabControl32", "", WsTabStop | WsClipSiblings, IdTab);
        for (var i = 0; i < FormatSettings.Tabs.Count; i++)
        {
            var text = _texts.Text("tab." + FormatSettings.Tabs[i]);
            fixed (char* p = text)
            {
                var item = new TabItem { Mask = 1, Caption = (nint)p }; // TCIF_TEXT
                SendMessageW(_tab, TcmInsertItemW, i, (nint)(&item));
            }
        }

        var tabs = FormatSettings.Tabs.ToList();
        var rowOfTab = new int[tabs.Count];
        for (var i = 0; i < FormatSettings.All.Count; i++)
        {
            var setting = FormatSettings.All[i];
            var tab = tabs.IndexOf(setting.Tab);
            var row = rowOfTab[tab]++;
            var label = _texts.Text("setting." + setting.Id);
            nint control;
            if (setting.Kind == SettingKind.Bool)
            {
                control = Child("BUTTON", label, WsTabStop | BsAutoCheckBox, IdSetting + i);
            }
            else
            {
                Placements.Add(new Placed(tab, row, Child("STATIC", label, 0, 0), IsLabel: true));
                control = Child("EDIT", "", WsTabStop | WsBorder | EsNumber, IdSetting + i);
            }
            Placements.Add(new Placed(tab, row, control, IsLabel: false));
            SettingControls[setting.Id] = control;
        }

        _view = CreateCodeView(_hwnd, IdPreview, _dpi);

        _save = Child("BUTTON", _texts.Text("button.save"), WsTabStop | BsDefPushButton, IdSave);
        _default = Child("BUTTON", _texts.Text("button.default"), WsTabStop | BsPushButton, IdDefault);
        _close = Child("BUTTON", _texts.Text("button.close"), WsTabStop | BsPushButton, IdClose);
        ShowTab(0);
    }

    private static nint Child(string cls, string text, uint style, int id)
    {
        var handle = CreateWindowExW(0, cls, text, WsChild | WsVisible | style, 0, 0, 10, 10, _hwnd, id, 0, 0);
        if (handle != 0 && _uiFont != 0) SendMessageW(handle, WmSetFont, _uiFont, 1);
        return handle;
    }

    /// <summary>Puts every control in its place for the current size of the window: the settings on top, the preview takes the rest.</summary>
    private static void Layout()
    {
        if (!GetClientRect(_hwnd, out var client)) return;
        int width = client.Right, height = client.Bottom;
        var gap = S(Gap);
        // The settings of the shown tab fill columns, so that the preview keeps the room.
        var count = Math.Max(1, Placements.Where(p => p.Tab == _shownTab).Select(p => p.Row + 1).DefaultIfEmpty(1).Max());
        var columns = Math.Clamp((width - 2 * gap) / S(MinColumnWidth), 1, 3);
        var rows = (count + columns - 1) / columns;
        var columnWidth = (width - 2 * gap - S(16)) / columns;
        var tabHeight = S(TabHeaderHeight);
        var groupHeight = S(20) + rows * S(RowHeight);
        var groupTop = gap + tabHeight - S(2);

        MoveWindow(_tab, gap, gap, width - 2 * gap, tabHeight, 1);
        foreach (var p in Placements)
        {
            var y = groupTop + S(24) + p.Row % rows * S(RowHeight);
            var x = gap + S(16) + p.Row / rows * columnWidth;
            if (p.IsLabel) MoveWindow(p.Window, x, y + S(3), S(160), S(20), 1);
            else if (IsNumberBox(p.Window)) MoveWindow(p.Window, x + S(168), y, S(70), S(22), 1);
            else MoveWindow(p.Window, x, y, columnWidth - S(12), S(22), 1);
        }

        var previewTop = groupTop + groupHeight + gap;
        var buttonsTop = height - gap - S(ButtonHeight);
        var viewTop = previewTop;
        var viewHeight = Math.Max(S(40), buttonsTop - gap - viewTop);
        MoveWindow(_view, gap, viewTop, width - 2 * gap, viewHeight, 1);

        MoveWindow(_save, gap, buttonsTop, S(ButtonWidth), S(ButtonHeight), 1);
        MoveWindow(_default, gap + S(ButtonWidth) + S(8), buttonsTop, S(ButtonWidth), S(ButtonHeight), 1);
        MoveWindow(_close, width - gap - S(ButtonWidth), buttonsTop, S(ButtonWidth), S(ButtonHeight), 1);
        // The view keeps its old picture when it only moves: repaint it and the window around it, or ghosts of the old layout stay.
        if (_view != 0)
        {
            UpdateScrollbars(_view);
            InvalidateRect(_view, 0, 1);
        }
        InvalidateRect(_hwnd, 0, 1);
    }

    private static bool IsNumberBox(nint window) => SettingControls.Any(c => c.Value == window && FormatSettings.Find(c.Key)?.Kind == SettingKind.Number);

    private static void ShowTab(int tab)
    {
        _shownTab = tab;
        foreach (var p in Placements) ShowWindow(p.Window, p.Tab == tab ? SwShow : SwHide);
    }

    private static void PushProfileToControls()
    {
        _updating = true;
        foreach (var setting in FormatSettings.All)
        {
            var control = SettingControls[setting.Id];
            if (setting.Kind == SettingKind.Bool) SendMessageW(control, BmSetCheck, _profile.Get(setting.Id), 0);
            else SetWindowTextW(control, _profile.Get(setting.Id).ToString(CultureInfo.InvariantCulture));
        }
        _updating = false;
    }

    private static void ReadControlsIntoProfile()
    {
        var buffer = stackalloc char[16];
        foreach (var setting in FormatSettings.All)
        {
            var control = SettingControls[setting.Id];
            if (setting.Kind == SettingKind.Bool)
            {
                _profile.Set(setting.Id, SendMessageW(control, BmGetCheck, 0, 0) == 1 ? 1 : 0);
            }
            else
            {
                var length = GetWindowTextW(control, buffer, 16);
                if (int.TryParse(new ReadOnlySpan<char>(buffer, length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)) _profile.Set(setting.Id, number);
            }
        }
    }

    private static void UpdatePreview(bool revealChange = false)
    {
        var options = _profile.ToOptions();
        SetCodeViewText(_view, PostFormatter.Format(FormatSample.Text, options), options.Margin, revealChange);
        // The parts of line wrapping do nothing while wrapping is off: grey them out.
        foreach (var id in WrapParts) EnableWindow(SettingControls[id], options.WrapLongLines ? 1 : 0);
    }

    private static readonly string[] WrapParts = ["wrapDecodeInPairs", "wrapBetween", "alignNamedArguments"];

    private static void OnCommand(int id, int notification)
    {
        switch (id)
        {
            case IdClose:
                DestroyWindow(_hwnd);
                break;
            case IdDefault:
                _profile = new FormatProfile();
                PushProfileToControls();
                UpdatePreview();
                break;
            case IdSave:
                ReadControlsIntoProfile();
                if (FormatSettingsStore.Save(_profile)) Ide.SetStatusMessage(_texts.Text("settings.saved"));
                FormatSettingsStore.Apply(_profile);
                break;
            case >= IdSetting and < IdPreview when !_updating && (notification == 0 || notification == EnChange): // a click or a typed number
                ReadControlsIntoProfile();
                UpdatePreview(revealChange: true);
                break;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case WmCommand:
                    OnCommand((int)(wParam & 0xFFFF), (int)((wParam >> 16) & 0xFFFF));
                    return 0;
                case WmNotify when lParam != 0 && ((nint*)lParam)[0] == _tab && *(int*)(lParam + 16) == TcnSelChange:
                    ShowTab((int)SendMessageW(_tab, TcmGetCurSel, 0, 0));
                    Layout();
                    return 0;
                case WmSize when wParam != SizeMinimized && _view != 0:
                    Layout();
                    return 0;
                case WmGetMinMaxInfo when lParam != 0:
                    ((int*)lParam)[6] = S(MinWidth) + FrameWidth(); // ptMinTrackSize
                    ((int*)lParam)[7] = S(MinHeight) + FrameHeight();
                    return 0;
                case WmCtlColorStatic:
                    // Labels and the frame on the window's own colour (the default would paint them white).
                    SetBkColor(wParam, GetSysColor(15)); // COLOR_BTNFACE
                    return GetSysColorBrush(15);
                case WmClose:
                    DestroyWindow(hwnd);
                    return 0;
                case WmDestroy:
                    PostQuitMessage(0);
                    return 0;
            }
        }
        catch
        {
            // Never let an exception reach the IDE through a window procedure.
        }
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private static bool Register()
    {
        fixed (char* name = ClassName)
        {
            var wc = new WndClassEx
            {
                Size = (uint)sizeof(WndClassEx),
                Callback = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WndProc,
                Instance = GetModuleHandleW(0),
                Cursor = LoadCursorW(0, 32512), // IDC_ARROW
                Background = 16, // COLOR_BTNFACE + 1
                ClassNameText = (nint)name,
            };
            return RegisterClassExW(in wc) != 0 || Marshal.GetLastPInvokeError() == 1410; // 1410: already registered
        }
    }

    private static int S(int pixels) => MulDiv(pixels, _dpi, 96);

    private static int Dpi()
    {
        try
        {
            return (int)GetDpiForSystem();
        }
        catch (EntryPointNotFoundException)
        {
            return 96;
        }
    }

    private static (int X, int Y) Center(nint owner, int width, int height)
    {
        if (owner != 0 && GetWindowRect(owner, out var r))
        {
            return (Math.Max(0, r.Left + (r.Right - r.Left - width) / 2), Math.Max(0, r.Top + (r.Bottom - r.Top - height) / 2));
        }
        return ((GetSystemMetrics(0) - width) / 2, (GetSystemMetrics(1) - height) / 2);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WndClassEx
    {
        public uint Size;
        public uint ClassStyle;
        public nint Callback;
        public int ClsExtra;
        public int WndExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassNameText;
        public nint IconSmall;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Window;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TabItem
    {
        public uint Mask;
        public uint State;
        public uint StateMask;
        public nint Caption;
        public int TextMax;
        public int Image;
        public nint Param;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InitCommonControls
    {
        public uint Size;
        public uint Classes;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    private static partial ushort RegisterClassExW(in WndClassEx wc);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowExW(uint exStyle, string className, string text, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    private static partial nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial void DestroyWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial void ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    private static partial void UpdateWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial void EnableWindow(nint window, int enable);

    [LibraryImport("user32.dll")]
    private static partial void SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial void MoveWindow(nint window, int x, int y, int width, int height, int repaint);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageW(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetWindowTextW(nint window, string text);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextW(nint window, char* buffer, int max);

    [LibraryImport("user32.dll")]
    private static partial int GetMessageW(out Msg message, nint window, uint min, uint max);

    [LibraryImport("user32.dll")]
    private static partial int IsDialogMessageW(nint dialog, ref Msg message);

    [LibraryImport("user32.dll")]
    private static partial int TranslateMessage(in Msg message);

    [LibraryImport("user32.dll")]
    private static partial nint DispatchMessageW(in Msg message);

    [LibraryImport("user32.dll")]
    private static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll")]
    private static partial nint LoadCursorW(nint instance, nint name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint window, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustWindowRectEx(ref Rect rect, uint style, int menu, uint exStyle);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();

    [LibraryImport("kernel32.dll")]
    private static partial nint GetModuleHandleW(nint name);

    [LibraryImport("kernel32.dll")]
    private static partial int MulDiv(int number, int numerator, int denominator);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut,
        uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

    [LibraryImport("gdi32.dll")]
    private static partial void DeleteObject(nint handle);

    [LibraryImport("gdi32.dll")]
    private static partial void SetBkColor(nint dc, uint color);

    [LibraryImport("user32.dll")]
    private static partial uint GetSysColor(int index);

    [LibraryImport("user32.dll")]
    private static partial nint GetSysColorBrush(int index);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InitCommonControlsEx(in InitCommonControls controls);
}
