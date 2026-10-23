using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TextTools.Core.Formatting;

namespace TextTools.Plugin;

/// <summary>
/// The preview of the settings window: read-only code with line numbers, a gutter, colours for keywords, literals and comments
/// (<see cref="SyntaxHighlighter"/>), a thin line at the right margin, trailing blanks drawn as dots so that a rule about them is
/// visible, and scrollbars. Drawn by hand (GDI) in a window class of its own: an EDIT control can show none of this.
/// </summary>
internal static unsafe partial class SettingsWindow
{
    private const string ViewClassName = "TextToolsCodeView";
    private const uint WmPaint = 0xF, WmEraseBkgnd = 0x14, WmVScroll = 0x115, WmHScroll = 0x114, WmMouseWheel = 0x20A;
    private const uint WsExClientEdge = 0x200;
    private const int SbHorz = 0, SbVert = 1, Pad = 8;
    private const uint ColorText = 0x000000, ColorKeyword = 0x808000, ColorLiteral = 0xC00000, ColorComment = 0x0000C0;
    private const uint ColorGutter = 0xF0F0F0, ColorGutterText = 0x808080, ColorBackground = 0xFFFFFF, ColorDots = 0xC8C8C8, ColorMargin = 0xE4E4E4;

    private static nint _viewFont;
    private static int _charWidth = 8, _lineHeight = 16, _viewTop, _viewLeft, _viewColumns, _viewMargin = 76;
    private static string[] _viewLines = [];
    private static CodeClass[][] _viewClasses = [];

    private static bool RegisterCodeView()
    {
        fixed (char* name = ViewClassName)
        {
            var wc = new WndClassEx
            {
                Size = (uint)sizeof(WndClassEx),
                Callback = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&ViewProc,
                Instance = GetModuleHandleW(0),
                Cursor = LoadCursorW(0, 32512), // IDC_ARROW
                ClassNameText = (nint)name,
            };
            return RegisterClassExW(in wc) != 0 || Marshal.GetLastPInvokeError() == 1410;
        }
    }

    private static nint CreateCodeView(nint parent, int id, int dpi)
    {
        _viewFont = CreateFontW(-MulDiv(10, dpi, 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Consolas");
        var view = CreateWindowExW(WsExClientEdge, ViewClassName, "", WsChild | WsVisible | WsVScroll | WsHScroll, 0, 0, 10, 10, parent, id, 0, 0);
        if (view == 0) return 0;
        var dc = GetDC(view);
        var old = SelectObject(dc, _viewFont);
        if (GetTextMetricsW(dc, out var metrics) != 0)
        {
            _lineHeight = Math.Max(8, metrics.Height + metrics.ExternalLeading);
            _charWidth = Math.Max(4, metrics.AveCharWidth);
        }
        SelectObject(dc, old);
        ReleaseDC(view, dc);
        return view;
    }

    /// <summary>Shows <paramref name="text"/> (lines separated by \n) with a line at column <paramref name="margin"/>.</summary>
    private static void SetCodeViewText(nint view, string text, int margin)
    {
        _viewLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        _viewClasses = new CodeClass[_viewLines.Length][];
        var all = SyntaxHighlighter.Classify(string.Join('\n', _viewLines));
        var offset = 0;
        for (var i = 0; i < _viewLines.Length; i++)
        {
            _viewClasses[i] = all[offset..(offset + _viewLines[i].Length)];
            offset += _viewLines[i].Length + 1;
        }
        _viewColumns = _viewLines.Max(l => l.Length) + 2;
        _viewMargin = margin;
        UpdateScrollbars(view);
        InvalidateRect(view, 0, 0);
    }

    private static (int Rows, int Columns, int Gutter) ViewMetrics(nint view)
    {
        GetClientRect(view, out var r);
        var gutter = Math.Max(2, _viewLines.Length.ToString(System.Globalization.CultureInfo.InvariantCulture).Length) * _charWidth + 2 * Pad;
        return (Math.Max(1, r.Bottom / _lineHeight), Math.Max(1, (r.Right - gutter - Pad) / _charWidth), gutter);
    }

    private static void UpdateScrollbars(nint view)
    {
        var (rows, columns, _) = ViewMetrics(view);
        _viewTop = Math.Clamp(_viewTop, 0, Math.Max(0, _viewLines.Length - rows));
        _viewLeft = Math.Clamp(_viewLeft, 0, Math.Max(0, _viewColumns - columns));
        var vertical = new ScrollInfo { Size = (uint)sizeof(ScrollInfo), Mask = 0xF, Max = Math.Max(0, _viewLines.Length - 1), Page = (uint)rows, Pos = _viewTop };
        var horizontal = new ScrollInfo { Size = (uint)sizeof(ScrollInfo), Mask = 0xF, Max = Math.Max(0, _viewColumns - 1), Page = (uint)columns, Pos = _viewLeft };
        SetScrollInfo(view, SbVert, in vertical, 1);
        SetScrollInfo(view, SbHorz, in horizontal, 1);
    }

    private static void Scroll(nint view, int bar, int code)
    {
        var (rows, columns, _) = ViewMetrics(view);
        var page = bar == SbVert ? rows : columns;
        var position = bar == SbVert ? _viewTop : _viewLeft;
        switch (code)
        {
            case 0: position--; break; // SB_LINEUP / SB_LINELEFT
            case 1: position++; break;
            case 2: position -= page; break;
            case 3: position += page; break;
            case 4 or 5: // SB_THUMBPOSITION / SB_THUMBTRACK
                var info = new ScrollInfo { Size = (uint)sizeof(ScrollInfo), Mask = 0x10 }; // SIF_TRACKPOS
                if (GetScrollInfo(view, bar, ref info) != 0) position = info.TrackPos;
                break;
        }
        if (bar == SbVert) _viewTop = position;
        else _viewLeft = position;
        UpdateScrollbars(view);
        InvalidateRect(view, 0, 0);
    }

    private static void PaintCodeView(nint view)
    {
        var dc = BeginPaint(view, out var paint);
        try
        {
            GetClientRect(view, out var client);
            int width = client.Right, height = client.Bottom;
            if (width <= 0 || height <= 0) return;
            var buffer = CreateCompatibleDC(dc);
            var bitmap = CreateCompatibleBitmap(dc, width, height);
            var oldBitmap = SelectObject(buffer, bitmap);
            var oldFont = SelectObject(buffer, _viewFont);
            SetBkMode(buffer, 1); // TRANSPARENT
            var (rows, _, gutter) = ViewMetrics(view);
            var textX = gutter + Pad;

            Fill(buffer, 0, 0, width, height, ColorBackground);
            Fill(buffer, 0, 0, gutter, height, ColorGutter);
            Fill(buffer, gutter, 0, 1, height, 0xC8C8C8);
            var marginX = textX + (_viewMargin - _viewLeft) * _charWidth;
            if (marginX > gutter && marginX < width) Fill(buffer, marginX, 0, 1, height, ColorMargin);

            SetTextColor(buffer, ColorGutterText);
            for (var r = 0; r <= rows && _viewTop + r < _viewLines.Length; r++)
            {
                var number = (_viewTop + r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Text(buffer, gutter - Pad - number.Length * _charWidth, r * _lineHeight, number);
            }

            IntersectClipRect(buffer, gutter + 1, 0, width, height);
            for (var r = 0; r <= rows && _viewTop + r < _viewLines.Length; r++)
            {
                DrawLine(buffer, _viewLines[_viewTop + r], _viewClasses[_viewTop + r], textX, r * _lineHeight);
            }
            SelectClipRgn(buffer, 0);

            BitBlt(dc, 0, 0, width, height, buffer, 0, 0, 0x00CC0020); // SRCCOPY
            SelectObject(buffer, oldFont);
            SelectObject(buffer, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(buffer);
        }
        finally
        {
            EndPaint(view, in paint);
        }
    }

    /// <summary>One line: runs of one colour; blanks at the end of the line as light dots.</summary>
    private static void DrawLine(nint dc, string line, CodeClass[] classes, int textX, int y)
    {
        var codeLength = line.TrimEnd(' ', '\t').Length;
        var start = 0;
        while (start < codeLength)
        {
            var end = start;
            while (end < codeLength && classes[end] == classes[start]) end++;
            if (end > _viewLeft)
            {
                var from = Math.Max(start, _viewLeft);
                SetTextColor(dc, classes[start] switch { CodeClass.Keyword => ColorKeyword, CodeClass.Literal => ColorLiteral, CodeClass.Comment => ColorComment, _ => ColorText });
                Text(dc, textX + (from - _viewLeft) * _charWidth, y, line[from..end]);
            }
            start = end;
        }
        if (line.Length > codeLength)
        {
            SetTextColor(dc, ColorDots);
            for (var i = Math.Max(codeLength, _viewLeft); i < line.Length; i++) Text(dc, textX + (i - _viewLeft) * _charWidth, y, "·");
        }
    }

    private static void Fill(nint dc, int x, int y, int width, int height, uint color)
    {
        var brush = CreateSolidBrush(color);
        var rect = new Rect { Left = x, Top = y, Right = x + width, Bottom = y + height };
        FillRect(dc, in rect, brush);
        DeleteObject(brush);
    }

    private static void Text(nint dc, int x, int y, string text)
    {
        fixed (char* p = text) TextOutW(dc, x, y, p, text.Length);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint ViewProc(nint view, uint message, nint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case WmPaint:
                    PaintCodeView(view);
                    return 0;
                case WmEraseBkgnd:
                    return 1; // everything is painted in the buffer
                case WmSize:
                    UpdateScrollbars(view);
                    return 0;
                case WmVScroll:
                    Scroll(view, SbVert, (int)(wParam & 0xFFFF));
                    return 0;
                case WmHScroll:
                    Scroll(view, SbHorz, (int)(wParam & 0xFFFF));
                    return 0;
                case WmMouseWheel:
                    _viewTop -= (short)((wParam >> 16) & 0xFFFF) / 120 * 3;
                    UpdateScrollbars(view);
                    InvalidateRect(view, 0, 0);
                    return 0;
                case WmDestroy:
                    if (_viewFont != 0) DeleteObject(_viewFont);
                    _viewFont = 0;
                    return 0;
            }
        }
        catch
        {
            // Never let an exception reach the IDE through a window procedure.
        }
        return DefWindowProcW(view, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        public uint Size;
        public uint Mask;
        public int Min;
        public int Max;
        public uint Page;
        public int Pos;
        public int TrackPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public nint Dc;
        public int Erase;
        public Rect Paint;
        public int Restore;
        public int IncUpdate;
        public fixed byte Reserved[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TextMetric
    {
        public int Height;
        public int Ascent;
        public int Descent;
        public int InternalLeading;
        public int ExternalLeading;
        public int AveCharWidth;
        public int MaxCharWidth;
        public int Weight;
        public int Overhang;
        public int DigitizedAspectX;
        public int DigitizedAspectY;
        public fixed byte Rest[16];
    }

    [LibraryImport("user32.dll")]
    private static partial nint BeginPaint(nint window, out PaintStruct paint);

    [LibraryImport("user32.dll")]
    private static partial void EndPaint(nint window, in PaintStruct paint);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial void ReleaseDC(nint window, nint dc);

    [LibraryImport("user32.dll")]
    private static partial void InvalidateRect(nint window, nint rect, int erase);

    [LibraryImport("user32.dll")]
    private static partial int FillRect(nint dc, in Rect rect, nint brush);

    [LibraryImport("user32.dll")]
    private static partial int SetScrollInfo(nint window, int bar, in ScrollInfo info, int redraw);

    [LibraryImport("user32.dll")]
    private static partial int GetScrollInfo(nint window, int bar, ref ScrollInfo info);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint dc, int width, int height);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint obj);

    [LibraryImport("gdi32.dll")]
    private static partial void DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial void SetBkMode(nint dc, int mode);

    [LibraryImport("gdi32.dll")]
    private static partial void SetTextColor(nint dc, uint color);

    [LibraryImport("gdi32.dll")]
    private static partial void TextOutW(nint dc, int x, int y, char* text, int count);

    [LibraryImport("gdi32.dll")]
    private static partial int GetTextMetricsW(nint dc, out TextMetric metrics);

    [LibraryImport("gdi32.dll")]
    private static partial void IntersectClipRect(nint dc, int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    private static partial void SelectClipRgn(nint dc, nint region);

    [LibraryImport("gdi32.dll")]
    private static partial void BitBlt(nint dest, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
}
