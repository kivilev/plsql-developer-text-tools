using System.Globalization;
using System.Text.RegularExpressions;
using TextTools.Core;
using TextTools.Core.Sql;

namespace TextTools.Plugin;

/// <summary>Applies a command to the editor of the current window.</summary>
internal static class Editor
{
    public static void Apply(Command cmd)
    {
        if (cmd is SettingsCommand)
        {
            // A window of its own: needs no editor and changes no text.
            SettingsWindow.Show(Ide.AppHandle);
            return;
        }
        if (!Ide.WindowHasEditor(false))
        {
            Status("no editor in the current window");
            return;
        }
        if (cmd is not (CopyCommand or PlanCommand) && Ide.GetReadOnly)
        {
            Status("editor is read-only");
            return;
        }
        if (!TextRoundTripsSafely(Ide.GetText()))
        {
            // Writing it back would replace characters with '?' / U+FFFD and silently corrupt the script.
            Status("text encoding not supported by the plug-in interface, nothing changed");
            return;
        }

        try
        {
            switch (cmd)
            {
                case TransformCommand t:
                    Transform(t);
                    break;
                case InsertCommand i:
                    Insert(i);
                    break;
                case CopyCommand c:
                    Copy(c);
                    break;
                case PlanCommand p:
                    Plan(p);
                    break;
            }
        }
        catch (FormatException e)
        {
            // Bad input (not a date, not Base64, ...) is a user-level message, not an error dialog.
            // A message without spaces is a stripped framework resource key (Native AOT) — replace it.
            Status(e.Message.Contains(' ', StringComparison.Ordinal) ? e.Message : $"{cmd.Name}: invalid input");
        }
        catch (RegexMatchTimeoutException)
        {
            // Every regex has Limits.RegexTimeoutMs: a pathological selection must not freeze the IDE.
            Status($"{cmd.Name}: selection too complex, nothing changed");
        }
    }

    /// <summary>Replaces the selection, or the whole text when nothing is selected.</summary>
    private static void Transform(TransformCommand cmd)
    {
        var text = Ide.GetText();
        var selected = Ide.GetSelectedText();
        int x = Ide.GetCursorX(), y = Ide.GetCursorY();

        if (selected.Length == 0)
        {
            Ide.SetText(cmd.Transform(text));
            Ide.SetCursor(x, y);
        }
        else
        {
            var r = SelectionLocator.Replace(text, selected, x, y, cmd.Transform);
            if (r is null)
            {
                Status("could not locate the selection, select a unique fragment");
                return;
            }
            Ide.SetText(r.Value.Text);
            Ide.SetCursor(r.Value.CursorX, r.Value.CursorY);
        }
        Status(cmd.StatusExample is { } example ? $"{cmd.Name}  (e.g. {example})" : cmd.Name);
    }

    /// <summary>Inserts a "now" value at the cursor, replacing the selection if there is one.</summary>
    private static void Insert(InsertCommand cmd)
    {
        var (now, source) = Now();
        var value = cmd.Format(now);
        var text = Ide.GetText();
        var selected = Ide.GetSelectedText();
        int x = Ide.GetCursorX(), y = Ide.GetCursorY();

        var r = selected.Length > 0
            ? SelectionLocator.Replace(text, selected, x, y, _ => value)
            : SelectionLocator.Insert(text, x, y, value);
        if (r is null)
        {
            Status("could not locate the cursor position");
            return;
        }
        Ide.SetText(r.Value.Text);
        Ide.SetCursor(r.Value.CursorX, r.Value.CursorY);
        Status($"{value} (from {source})");
    }

    /// <summary>The selection (or the statement under the cursor) with constants resolved, as Copy SQL does it.</summary>
    private static (CopyResult Result, DatabaseSpecs? Database) Resolve(Func<SqlRequest, ISpecSource?, CopyResult> build)
    {
        var text = Ide.GetText();
        var selected = Ide.GetSelectedText();
        int x = Ide.GetCursorX(), y = Ide.GetCursorY();
        var start = selected.Length > 0 ? SelectionLocator.Locate(text, selected, x, y) : 0;
        var database = DatabaseSpecs.IfConnected();
        return (build(new SqlRequest(text, start, selected.Length, SelectionLocator.ToOffset(text, x, y)), database), database);
    }

    /// <summary>
    /// Explain plan of the resolved statement: in the IDE's own Plan Window (executed at once), or in the plug-in's
    /// HTML window with the Plan and Findings tabs. The IDE Plan Window needs a connection; the HTML one explains why
    /// there is no plan instead.
    /// </summary>
    private static void Plan(PlanCommand cmd)
    {
        var (result, _) = Resolve(ConstantResolver.CopySql);
        if (cmd.View == PlanView.IdeWindow)
        {
            if (!Ide.Connected)
            {
                Status("Explain Plan needs a database connection");
                return;
            }
            Ide.CreateWindow(5, result.Text, execute: !Ide.WindowIsRunning); // Plan Window
            Status($"Explain Plan: {result.Summary}");
            return;
        }
        Status(PlanWindow.Show(result.Text) ? $"Extended Explain Plan: {result.Summary}" : "Extended Explain Plan: the IDE could not open the window");
    }

    /// <summary>Puts the result for the selection (or the statement under the cursor) on the clipboard; the editor is not changed.</summary>
    private static void Copy(CopyCommand cmd)
    {
        var (result, database) = Resolve(cmd.Build);
        if (!Clipboard.SetText(Ide.AppHandle, result.Text))
        {
            Status("the clipboard is busy, try again");
            return;
        }
        Status($"SQL copied: {result.Summary}{DatabaseNote(database, result)}");
    }

    /// <summary>"(not connected ...)" when constants became binds offline; the time of dictionary queries when connected.</summary>
    private static string DatabaseNote(DatabaseSpecs? database, CopyResult result)
    {
        if (database is null)
        {
            return result.Summary.Contains("constants as binds", StringComparison.Ordinal) ? " (not connected to a database)" : "";
        }
        return database.DatabaseTime > TimeSpan.Zero
            ? string.Create(CultureInfo.InvariantCulture, $" (database {database.DatabaseTime.TotalMilliseconds:F0} ms)")
            : "";
    }

    /// <summary>
    /// Database time when connected (SYSTIMESTAMP), otherwise the PC clock. The query runs in the main IDE session,
    /// so it is skipped while the current window is executing (it would wait for the user's statement), and any
    /// failure falls back to the PC clock instead of an error.
    /// </summary>
    private static (DateTimeOffset Now, string Source) Now()
    {
        try
        {
            if (Ide.Connected && !Ide.WindowIsRunning)
            {
                var s = Ide.QuerySingleValue("select to_char(systimestamp, 'YYYY-MM-DD\"T\"HH24:MI:SS.FF6TZH:TZM') from dual");
                if (s is not null && DateTimeOffset.TryParseExact(s, "yyyy-MM-dd'T'HH:mm:ss.ffffffzzz",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var db))
                {
                    return (db, "database");
                }
            }
        }
        catch
        {
            // fall through to the PC clock
        }
        return (DateTimeOffset.Now, "PC clock");
    }

    /// <summary>
    /// Text can be rewritten only if it survived the trip through the char* interface: in UTF-8 mode undecodable
    /// bytes show up as U+FFFD; without UTF-8 mode any non-ASCII character may already be lost.
    /// </summary>
    private static bool TextRoundTripsSafely(string text) =>
        !text.Contains('�') && (Ide.Utf8 || text.All(char.IsAscii));

    private static void Status(string message) => Ide.SetStatusMessage($"{Menu.Title}: {message}");
}
