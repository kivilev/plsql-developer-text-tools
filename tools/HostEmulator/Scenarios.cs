using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace TextTools.HostEmulator;

/// <summary>One scenario per mode; each prints its checks and returns false if any failed.</summary>
internal static class Scenarios
{
    public static readonly string[] Modes = ["smoke", "nocallbacks", "noutf8", "unload", "stress", "selftest"];

    private static int _failures;

    public static bool Run(string mode, PluginHost host, string dll)
    {
        _failures = 0;
        switch (mode)
        {
            case "smoke": Smoke(host); break;
            case "nocallbacks": NoCallbacks(host); break;
            case "noutf8": NoUtf8(host); break;
            case "unload": Unload(host, dll); break;
            case "stress": Stress(host); break;
            case "selftest": SelfTest(host); break;
            default: throw new ArgumentException($"unknown mode {mode}", nameof(mode));
        }
        return _failures == 0;
    }

    private static void Check(bool condition, string what)
    {
        Console.WriteLine($"  {(condition ? "ok  " : "FAIL")} {what}");
        if (!condition) _failures++;
    }

    private static void Smoke(PluginHost host)
    {
        Check(string.Equals(host.Identity, "Text Tools for PL/SQL Developer", StringComparison.Ordinal), "IdentifyPlugIn returns the product name");
        Check(host.AboutText.Contains("Author", StringComparison.Ordinal), "About text");
        Check(host.Menu.ContainsKey("MENUITEM=String Tools") && host.Menu.ContainsKey("MENUITEM=Date Tools")
              && host.Menu.ContainsKey("MENUITEM=SQL Tools"), "three submenus");
        Check(FakeIde.ToolButtons.Count == 3 && FakeIde.ToolButtons.All(b =>
                host.Menu.TryGetValue("MENUITEM=" + b.Name, out var i) && i == b.Index
                && File.Exists(b.File) && File.ReadAllBytes(b.File).AsSpan(1, 3).SequenceEqual("PNG"u8)),
            "ribbon buttons get PNG images at their menu indices");
        var copyIndex = host.Menu["SUBITEM=Copy SQL with constants resolved"];
        var copyPopups = FakeIde.PopupItems.Where(p => p.Index == copyIndex).ToList();
        Check(copyPopups.Select(p => p.ObjectType).Order(StringComparer.Ordinal).SequenceEqual(["PROGRAMWINDOW", "SQLWINDOW", "TESTWINDOW"], StringComparer.Ordinal)
              && copyPopups.All(p => string.Equals(p.Name, "Copy SQL with constants resolved", StringComparison.Ordinal)),
            "Copy SQL is a top-level item of the editor context menu in Program, SQL and Test windows");
        Check(FakeIde.PopupItems.Count == 9 * 3
              && FakeIde.PopupItems.All(p => host.Menu.TryGetValue("SUBITEM=" + p.Name, out var i) && i == p.Index)
              && FakeIde.PopupItems.Any(p => string.Equals(p.Name, "List -> IN (...)", StringComparison.Ordinal)),
            "a short flat list of everyday commands in the context menu, each at its menu index");

        Check(string.Equals(host.Click("snake_case", "select firstName, клиентИмя from t", "firstName, клиентИмя", 28),
            "select first_name, клиент_имя from t", StringComparison.Ordinal), "snake_case on a selection with Cyrillic");
        Check(FakeIde.LastStatus.Contains("e.g. firstName -> first_name", StringComparison.Ordinal), "status bar shows the example");

        Check(string.Equals(host.Click("List -> IN (...)", "where id\r\nO'Neil\r\nIvanov", "O'Neil\r\nIvanov", 7, 3),
            "where id\r\nIN ('O''Neil', 'Ivanov')", StringComparison.Ordinal), "IN list from a multi-line selection");

        var inserted = host.Click("Now: DATE '2026-10-13'", "where d > ;", "", 11);
        Check(inserted.StartsWith("where d > DATE '", StringComparison.Ordinal) && inserted.EndsWith("';", StringComparison.Ordinal), "Now inserts at the cursor");
        Check(FakeIde.LastStatus.Contains("PC clock", StringComparison.Ordinal), "no DB connection: PC clock");

        Check(string.Equals(host.Click("ISO duration -> INTERVAL", "PT1H30M"), "INTERVAL '1:30' HOUR TO MINUTE", StringComparison.Ordinal), "ISO duration");

        var before = "01/02/2026";
        Check(string.Equals(host.Click("Date -> DATE / TIMESTAMP literal", before), before, StringComparison.Ordinal)
              && FakeIde.LastStatus.Contains("ambiguous", StringComparison.Ordinal), "ambiguous date: text kept, status explains");

        Check(string.Equals(host.Click("Base64 decode", "!!!"), "!!!", StringComparison.Ordinal)
              && FakeIde.LastStatus.Contains("not valid Base64", StringComparison.Ordinal), "bad Base64: readable status, no dialog");

        Check(string.Equals(host.Click("snake_case", "x x", "x", 50, 50), "x x", StringComparison.Ordinal), "cursor outside the text: nothing corrupted");

        const string package = "declare\r\n  c_new constant varchar2(3) := 'НОВ';\r\nbegin\r\n  delete from orders where status = c_new;\r\nend;";
        Check(string.Equals(host.Click("Copy SQL with constants resolved", package, "", 10, 4), package, StringComparison.Ordinal)
              && string.Equals(ClipboardText(), "delete from orders where status = 'НОВ' /*c_new*/", StringComparison.Ordinal)
              && FakeIde.LastStatus.Contains("1 constant", StringComparison.Ordinal),
            "Copy SQL: statement under the cursor on the clipboard (Cyrillic), editor unchanged");
        var clipboard = ClipboardText();
        Check(string.Equals(host.Click("Copy SQL with constants resolved", "begin null; end;", "", 3, 1), "begin null; end;", StringComparison.Ordinal)
              && string.Equals(ClipboardText(), clipboard, StringComparison.Ordinal)
              && FakeIde.LastStatus.Contains("put the cursor in a SQL statement", StringComparison.Ordinal),
            "Copy SQL without a statement: readable status, clipboard kept");

        const string query = "select * from orders o where o.type_id = pkg_const.c_retail and o.state = pkg_const.c_unknown";
        host.Click("Copy SQL with constants resolved", query, "", 5, 1);
        Check(string.Equals(ClipboardText(), "select * from orders o where o.type_id = :c_retail and o.state = :c_unknown", StringComparison.Ordinal)
              && FakeIde.LastStatus.Contains("not connected", StringComparison.Ordinal) && FakeIde.Queries.Count == 0,
            "Copy SQL offline: package constants become binds, no queries");
        (FakeIde.Connected, FakeIde.Rows) = (true, ["package pkg_const is\n", "  c_retail constant number := 10;\n", "end;\n"]);
        host.Click("Copy SQL with constants resolved", query, "", 5, 1);
        (FakeIde.Connected, FakeIde.Rows) = (false, []);
        Check(string.Equals(ClipboardText(), "select * from orders o where o.type_id = 10 /*pkg_const.c_retail*/ and o.state = pkg_const.c_unknown", StringComparison.Ordinal)
              && FakeIde.Queries.Count == 1 && FakeIde.Queries[0].Contains("'PKG_CONST'", StringComparison.Ordinal)
              && !FakeIde.Queries[0].Contains("all_objects", StringComparison.Ordinal)
              && FakeIde.LastStatus.Contains("database", StringComparison.Ordinal),
            "Copy SQL connected: one indexed dictionary query, the value from the package specification, time in the status");
        (FakeIde.Connected, FakeIde.Rows) = (true, ["package pkg_const is\n", "  c_retail constant number := 10;\n", "end;\n"]);
        host.Click("Copy SQL with constants resolved", query, "", 5, 1);
        (FakeIde.Connected, FakeIde.Rows) = (false, []);
        Check(FakeIde.Queries.Count == 1, "Copy SQL again within 5 seconds: the specification comes from the cache");
    }

    private static string ClipboardText()
    {
        if (!OpenClipboard(0)) return "<clipboard busy>";
        try
        {
            var handle = GetClipboardData(13 /* CF_UNICODETEXT */);
            var p = handle == 0 ? 0 : GlobalLock(handle);
            if (p == 0) return "";
            try { return Marshal.PtrToStringUni(p) ?? ""; }
            finally { GlobalUnlock(handle); }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern nint GetClipboardData(uint format);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint memory);

    /// <summary>
    /// The self test the CI runs in the real IDE (TEXTTOOLS_SELFTEST + AfterStart, a timer on the message loop), here
    /// against the fake callbacks; then the Command Window form "TextTools selftest".
    /// </summary>
    private static void SelfTest(PluginHost host)
    {
        var result = Path.Combine(Path.GetTempPath(), $"texttools-selftest-{Environment.ProcessId}.json");
        File.Delete(result);
        Environment.SetEnvironmentVariable("TEXTTOOLS_SELFTEST", result);
        unsafe
        {
            ((delegate* unmanaged[Cdecl]<void>)NativeLibrary.GetExport(host.Library, "AfterStart"))();
        }
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(result) && DateTime.UtcNow < deadline)
        {
            while (PeekMessageW(out var msg, 0, 0, 0, 1 /* PM_REMOVE */))
            {
                DispatchMessageW(in msg);
            }
            Thread.Sleep(50);
        }
        Environment.SetEnvironmentVariable("TEXTTOOLS_SELFTEST", null);
        var json = File.Exists(result) ? File.ReadAllText(result) : "";
        File.Delete(result);
        Console.WriteLine($"  {json.Split('\n').FirstOrDefault()}");
        Check(json.Contains("\"failed\":0,", StringComparison.Ordinal) && FakeIde.CreatedWindows.Count == 1 && FakeIde.CreatedWindows[0].Type == 1,
            "TEXTTOOLS_SELFTEST: after AfterStart the timer runs every command in a new SQL Window, all passed, JSON written");
        foreach (var line in json.Split('\n').Where(l => l.Contains("\"ok\":false", StringComparison.Ordinal)).Take(10))
        {
            Console.WriteLine($"    {line}");
        }

        unsafe
        {
            var commandLine = (delegate* unmanaged[Cdecl]<int, byte*, byte*, void>)NativeLibrary.GetExport(host.Library, "CommandLine");
            fixed (byte* selftest = "selftest\0"u8, empty = "\0"u8, other = "frobnicate\0"u8)
            {
                commandLine(42, selftest, empty);
                commandLine(42, other, empty);
            }
        }
        Check(FakeIde.Feedback.Count >= 2 && FakeIde.Feedback[0].EndsWith(" 0 failed", StringComparison.Ordinal)
              && FakeIde.Feedback[^1].Contains("unknown command", StringComparison.Ordinal),
            "Command Window: 'TextTools selftest' reports the summary, an unknown command explains itself");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out Msg msg, nint hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(in Msg msg);

    private static void NoCallbacks(PluginHost host)
    {
        for (var i = -5; i <= 120; i++)
        {
            host.Click(i);
        }
        Check(condition: true, "IDE without callbacks: every index clicked, process alive");
    }

    private static void NoUtf8(PluginHost host)
    {
        // OnActivate already ran with AcceptUtf8 = true; re-run the decision with a refusing IDE.
        FakeIde.AcceptUtf8 = false;
        var onActivate = NativeLibrary.GetExport(host.Library, "OnActivate");
        unsafe
        {
            ((delegate* unmanaged[Cdecl]<void>)onActivate)();
        }
        const string text = "select имя_клиента from t";
        Check(string.Equals(host.Click("camelCase", text), text, StringComparison.Ordinal), "non-ASCII text without UTF-8 mode is not rewritten");
        Check(FakeIde.LastStatus.Contains("encoding", StringComparison.Ordinal), "status explains why");
        Check(string.Equals(host.Click("camelCase", "first_name"), "firstName", StringComparison.Ordinal), "ASCII text still works");
    }

    private static void Unload(PluginHost host, string dll)
    {
        host.Click("snake_case", "firstName");
        for (var i = 0; i < 3; i++)
        {
            NativeLibrary.Free(host.Library);   // what an IDE may do when the plugin is disabled or on exit
        }
        for (var i = 0; i < 20; i++)
        {
            _ = new byte[10_000_000];
            GC.Collect();
            Thread.Sleep(100);
        }
        Check(GetModuleHandleW(Path.GetFileName(dll)) != 0, "FreeLibrary x3 + GC pressure: module stays pinned, process alive");
    }

    private static void Stress(PluginHost host)
    {
        var random = new Random(7);
        string[] samples = ["select first_name, last_name from t", "13.10.2026 14:30", "1760356800", "PT1H30M",
            "yyyy-MM-dd'T'HH:mm:ss", "it's", "клиент_имя\r\nO'Neil\r\n42", "  l_x := 1;\r\n  l_yy:=2;", "SGVsbG8=", "",
            new string('x', 20_000), string.Join("\r\n", Enumerable.Range(1, 3000))];
        var commands = host.GetCommandIndices();

        void Round(int clicks)
        {
            for (var k = 0; k < clicks; k++)
            {
                var text = samples[random.Next(samples.Length)];
                var useSelection = text.Length > 0 && random.Next(2) == 0;
                var start = useSelection ? random.Next(text.Length) : 0;
                var length = useSelection ? random.Next(1, Math.Min(30, text.Length - start) + 1) : 0;
                (FakeIde.Text, FakeIde.Selected) = (text, useSelection ? text.Substring(start, length) : "");
                (FakeIde.X, FakeIde.Y) = (random.Next(-2, 40), random.Next(-2, 5));
                host.Click(commands[random.Next(commands.Count)]);
            }
        }

        static long PrivateBytes()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var p = Process.GetCurrentProcess();
            return p.PrivateMemorySize64;
        }

        // The plugin's GC sizes its heap to the machine (cores, regions): the first rounds grow memory by 10-30 MB on
        // CI runners without any leak, and later checkpoints fluctuate by a few MB. A leak is a steady trend, so skip
        // the warm-up and take the least-squares slope of the remaining checkpoints.
        var checkpoints = new List<double>();
        for (var i = 0; i < Checkpoints; i++)
        {
            Round(ClicksPerCheckpoint);
            checkpoints.Add(PrivateBytes() / 1048576.0);
        }
        var slope = Slope(checkpoints.Skip(WarmUpCheckpoints).ToList());
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {Checkpoints * ClicksPerCheckpoint / 1000}k random clicks, private MB per {ClicksPerCheckpoint / 1000}k: {string.Join(" ", checkpoints.Select(b => b.ToString("F1", CultureInfo.InvariantCulture)))}"));
        Check(slope < LeakSlopeMbPerCheckpoint, string.Create(CultureInfo.InvariantCulture,
            $"no leak: trend {slope:F2} MB per {ClicksPerCheckpoint / 1000}k clicks after warm-up (limit {LeakSlopeMbPerCheckpoint})"));
    }

    private const int Checkpoints = 12;
    private const int WarmUpCheckpoints = 3;
    private const int ClicksPerCheckpoint = 25_000;

    /// <summary>
    /// 1.5 MB per 25k clicks = ~60 bytes per click. GitHub runners show a steady ~0.6 MB per 25k of heap creep without
    /// any leak (none locally), so the limit leaves 2.5x headroom over that. Calibrated with a leak injected into every
    /// SetText: 100 B gives ~1.9 (caught), 50 B ~1.2 (not caught). A real leak here would be an unfreed text buffer:
    /// hundreds of bytes to kilobytes per click.
    /// </summary>
    private const double LeakSlopeMbPerCheckpoint = 1.5;

    private static double Slope(List<double> ys)
    {
        var meanX = (ys.Count - 1) / 2.0;
        var meanY = ys.Average();
        double num = 0, den = 0;
        for (var x = 0; x < ys.Count; x++)
        {
            num += (x - meanX) * (ys[x] - meanY);
            den += (x - meanX) * (x - meanX);
        }
        return num / den;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string name);
}
