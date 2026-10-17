using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using TextTools.Core;
using TextTools.Core.Sql;

namespace TextTools.Plugin;

/// <summary>
/// Runs every command through the real IDE callbacks (a new SQL Window, IDE_SetText / IDE_GetText, the cursor, the
/// clipboard) and reports the results: what the host emulator cannot prove - real callback indices and signatures,
/// charset round trips, the window the IDE creates. Started two ways:
///  - CI: environment variable TEXTTOOLS_SELFTEST=&lt;result.json&gt;; after AfterStart a timer waits until the IDE main
///    window is enabled (no modal first-start dialog), runs once and writes the JSON (scripts/real-ide-smoke.ps1);
///  - by hand: "TextTools selftest [result.json]" in a Command Window; the summary goes back to the Command Window.
/// It creates a SQL Window and the clipboard is overwritten: never started without being asked.
/// </summary>
internal static unsafe partial class SelfTest
{
    public const string EnvironmentVariable = "TEXTTOOLS_SELFTEST";
    private const int TimerMs = 2000;
    private const int MaxTicks = 300; // 10 minutes: then report that the IDE never became ready
    private const int NoMainFormTicks = 3; // no TPLSQLDevForm at all (the host emulator): idle after 6 s

    private static nuint _timer;
    private static int _ticks;

    private sealed record Check(string Name, bool Ok, string Detail);

    /// <summary>AfterStart: arms the timer when TEXTTOOLS_SELFTEST is set.</summary>
    public static void StartIfRequested()
    {
        if (_timer != 0 || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariable))) return;
        _timer = SetTimer(0, 0, TimerMs, &OnTimer);
    }

    [UnmanagedCallersOnly]
    private static void OnTimer(nint hwnd, uint message, nuint id, uint time)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable(EnvironmentVariable) ?? "";
            if (++_ticks < MaxTicks && !IdeIsIdle()) return;
            KillTimer(0, _timer);
            if (_ticks >= MaxTicks)
            {
                Write(path, [new Check("ide ready", false, "the IDE main window never became enabled (a modal dialog?)")]);
                return;
            }
            Write(path, Run());
        }
        catch
        {
            // the IDE must survive; a missing result file fails the CI job
        }
    }

    /// <summary>Command Window: "TextTools selftest [path]".</summary>
    public static void FromCommandWindow(int feedback, string parameters)
    {
        var clipboard = Clipboard.GetText(Ide.AppHandle);
        var checks = Run();
        if (clipboard is not null) Clipboard.SetText(Ide.AppHandle, clipboard);
        var path = parameters.Trim();
        if (path.Length > 0)
        {
            try
            {
                Write(path, checks);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Ide.CommandFeedback(feedback, $"Text Tools self test: cannot write {path}: {e.Message}");
            }
        }
        var failed = checks.Where(c => !c.Ok).ToList();
        Ide.CommandFeedback(feedback, $"Text Tools self test: {checks.Count - failed.Count} passed, {failed.Count} failed");
        foreach (var c in failed) Ide.CommandFeedback(feedback, $"  FAIL {c.Name}: {c.Detail}");
    }

    private static List<Check> Run()
    {
        var checks = new List<Check>
        {
            new("ide version", Ide.SysVersion > 0, Ide.SysVersion.ToString(CultureInfo.InvariantCulture)),
            new("utf8 mode", Ide.Utf8, Ide.Utf8 ? "CHARMODE=UTF8 accepted" : "the IDE refused CHARMODE=UTF8"),
        };

        const string marker = "select 'Text Tools self test' from dual";
        Ide.CreateWindow(1, marker); // SQL Window
        var window = Ide.GetText();
        checks.Add(new Check("create sql window", Ide.WindowHasEditor(false) && Same(Normalize(window), marker), Normalize(window)));
        if (!checks[^1].Ok) return checks; // nothing else can work without the editor

        const string cyrillic = "select 'клиент' as имя from dual";
        Ide.SetText(cyrillic);
        checks.Add(new Check("cyrillic round trip", Same(Normalize(Ide.GetText()), cyrillic), Normalize(Ide.GetText())));

        foreach (var cmd in Commands.All)
        {
            checks.Add(Safe(cmd.Name, () => cmd switch
            {
                TransformCommand t => Transform(t),
                InsertCommand i => Insert(i),
                CopyCommand c => Copy(c),
                PlanCommand p => Plan(p),
                _ => new Check(cmd.Name, false, "unknown command type"),
            }));
        }
        Ide.SetText("-- Text Tools self test: " + checks.Count(c => c.Ok) + "/" + checks.Count + " passed");
        return checks;
    }

    private static Check Transform(TransformCommand cmd)
    {
        Ide.SetText(cmd.Sample ?? "x");
        var input = Ide.GetText(); // as the IDE stores it (line endings may change)
        Ide.SetCursor(1, 1);
        Editor.Apply(cmd);
        var actual = Ide.GetText();
        var expected = cmd.Transform(input);
        var ok = Same(actual, expected);
        return new Check(cmd.Name, ok, ok ? Short(actual) : $"expected {Short(expected)}, got {Short(actual)}");
    }

    private static Check Insert(InsertCommand cmd)
    {
        const string before = "where d > ";
        Ide.SetText(before);
        Ide.SetCursor(before.Length + 1, 1);
        Editor.Apply(cmd);
        var actual = Ide.GetText();
        var ok = actual.StartsWith(before, StringComparison.Ordinal) && actual.Length > before.Length;
        return new Check(cmd.Name, ok, Short(actual));
    }

    private static Check Copy(CopyCommand cmd)
    {
        Ide.SetText(cmd.Sample);
        var input = Ide.GetText();
        var offset = input.IndexOf("select", StringComparison.Ordinal);
        var (x, y) = SelectionLocator.ToPosition(input, offset);
        Ide.SetCursor(x, y);
        Clipboard.SetText(Ide.AppHandle, "");
        Editor.Apply(cmd);
        var copied = Clipboard.GetText(Ide.AppHandle) ?? "<clipboard unreadable>";
        var expected = cmd.Build(new SqlRequest(input, 0, 0, offset), null).Text;
        var unchanged = Same(Ide.GetText(), input);
        var ok = Same(copied, expected) && unchanged;
        if (ok) return new Check(cmd.Name, true, Short(copied));
        var editor = unchanged ? "" : "; the editor text changed";
        return new Check(cmd.Name, false, $"expected {Short(expected)}, got {Short(copied)}{editor}");
    }

    /// <summary>
    /// Extended: the HTML window opens (the current window becomes an HTML one, type 7), connected or not.
    /// IDE window: needs a connection (the self test usually runs not logged on) - then it only checks the refusal.
    /// Afterwards a new SQL Window, so the next commands have an editor again.
    /// </summary>
    private static Check Plan(PlanCommand cmd)
    {
        Ide.SetText("select * from dual where dummy = 'X'");
        Ide.SetCursor(1, 1);
        var connected = Ide.Connected;
        Editor.Apply(cmd);
        var type = Ide.GetWindowType();
        var check = cmd.View switch
        {
            PlanView.Extended => new Check(cmd.Name, type == 7, $"window type {type.ToString(CultureInfo.InvariantCulture)} (7 = HTML)"),
            _ when connected => new Check(cmd.Name, type == 5, $"window type {type.ToString(CultureInfo.InvariantCulture)} (5 = Plan)"),
            _ => new Check(cmd.Name, type != 5, "not logged on: no Plan Window, a status message instead"),
        };
        Ide.CreateWindow(1, "");
        return check;
    }

    private static Check Safe(string name, Func<Check> check)
    {
        try
        {
            return check();
        }
        catch (Exception e)
        {
            return new Check(name, false, $"{e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>The IDE main form (TPLSQLDevForm) exists and is enabled: no modal dialog is open. No main form (the
    /// host emulator) counts as idle.</summary>
    private static bool IdeIsIdle()
    {
        nint main = 0;
        EnumWindows(&FindMain, (nint)(&main));
        return main == 0 ? _ticks >= NoMainFormTicks : IsWindowVisible(main) && IsWindowEnabled(main);
    }

    [UnmanagedCallersOnly]
    private static int FindMain(nint hwnd, nint state)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != (uint)Environment.ProcessId) return 1;
            var name = stackalloc char[64];
            var n = GetClassNameW(hwnd, name, 64);
            if (n <= 0 || !Same(new string(name, 0, n), "TPLSQLDevForm")) return 1;
            *(nint*)state = hwnd;
            return 0;
        }
        catch
        {
            return 0; // stop enumerating; never let an exception reach user32
        }
    }

    private static void Write(string path, List<Check> checks)
    {
        var json = new StringBuilder();
        json.Append(CultureInfo.InvariantCulture, $"{{\"passed\":{checks.Count(c => c.Ok)},\"failed\":{checks.Count(c => !c.Ok)},\"checks\":[");
        for (var i = 0; i < checks.Count; i++)
        {
            var c = checks[i];
            if (i > 0) json.Append(',');
            json.Append(CultureInfo.InvariantCulture, $"\n{{\"name\":{Quote(c.Name)},\"ok\":{(c.Ok ? "true" : "false")},\"detail\":{Quote(c.Detail)}}}");
        }
        json.Append("\n]}\n");
        File.WriteAllText(path, json.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ') sb.Append(CultureInfo.InvariantCulture, $"\\u{(int)ch:x4}");
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    private static string Normalize(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    private static string Short(string s) => s.Length <= 120 ? s : s[..120] + "...";

    [LibraryImport("user32.dll")]
    private static partial nuint SetTimer(nint hwnd, nuint id, uint ms, delegate* unmanaged<nint, uint, nuint, uint, void> callback);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool KillTimer(nint hwnd, nuint id);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(delegate* unmanaged<nint, nint, int> callback, nint state);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    private static partial int GetClassNameW(nint hwnd, char* name, int max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowEnabled(nint hwnd);
}
