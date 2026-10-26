using System.Text;
using TextTools.Core.Formatting;

namespace TextTools.Plugin;

/// <summary>
/// Command Window: <c>TextTools format &lt;input folder&gt; &lt;beautifier folder&gt; &lt;result folder&gt;</c>. Every file of the input folder
/// (package, type and body files) goes through the IDE's own Beautifier with the rules that are set in the IDE (written to the
/// first output folder), and then through the Text Tools rules of the current profile (written to the second one). It is how
/// the whole formatting, the native rules plus ours, is compared with another formatter on the same files.
/// Nothing is written into the input folder; the IDE editor is not touched.
/// </summary>
internal static class BatchFormat
{
    private static readonly string[] Extensions = [".pks", ".pkb", ".tps", ".tpb", ".sql"];

    public static void Run(int feedback, string parameters)
    {
        var args = SplitArguments(parameters);
        if (args.Count != 3)
        {
            Ide.CommandFeedback(feedback, "Text Tools format: usage: format <input folder> <beautifier output folder> <result folder>");
            return;
        }
        var (input, native, result) = (args[0], args[1], args[2]);
        if (!Directory.Exists(input))
        {
            Ide.CommandFeedback(feedback, $"Text Tools format: no such folder {input}");
            return;
        }
        if (Ide.BeautifyText("select 1 from dual;") is null)
        {
            Ide.CommandFeedback(feedback, "Text Tools format: this IDE has no Beautifier callback (IDE_BeautifyText)");
            return;
        }
        Directory.CreateDirectory(native);
        Directory.CreateDirectory(result);

        var files = Directory.EnumerateFiles(input).Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase).ToList();
        int done = 0, failed = 0, guarded = 0, retried = 0;
        foreach (var file in files)
        {
            try
            {
                var source = File.ReadAllText(file, Encoding.UTF8);
                var crlf = source.Contains("\r\n", StringComparison.Ordinal);
                // The editor of the IDE keeps CRLF; when the Beautifier gives nothing for the text as it is, it gets it that way once more.
                var beautified = Ide.BeautifyText(source);
                var needsCrlf = false;
                if (string.IsNullOrWhiteSpace(beautified) && !crlf)
                {
                    beautified = Ide.BeautifyText(source.Replace("\n", "\r\n", StringComparison.Ordinal));
                    needsCrlf = true;
                }
                if (string.IsNullOrWhiteSpace(beautified))
                {
                    failed++;
                    if (failed <= 10) Ide.CommandFeedback(feedback, $"  FAIL {Path.GetFileName(file)}: the Beautifier returned nothing ({source.Length} characters)");
                    continue;
                }
                if (needsCrlf) retried++;
                var accepted = PostFormatter.TryFormat(beautified, null, out var after);
                var formatted = accepted ? after : beautified;
                if (!accepted) guarded++;
                Write(Path.Combine(native, Path.GetFileName(file)), beautified, crlf);
                Write(Path.Combine(result, Path.GetFileName(file)), formatted, crlf);
                done++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                failed++;
                Ide.CommandFeedback(feedback, $"  FAIL {Path.GetFileName(file)}: {e.Message}");
            }
        }
        Ide.CommandFeedback(feedback, $"Text Tools format: {done} of {files.Count} files done, {failed} failed, {guarded} left as the Beautifier made them (the token check objected), {retried} needed CRLF");
    }

    /// <summary>The line endings of the source are kept, so that a file compares line by line with the same file of another formatter.</summary>
    private static void Write(string path, string text, bool crlf)
    {
        var lf = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, crlf ? lf.Replace("\n", "\r\n", StringComparison.Ordinal) : lf, new UTF8Encoding(false));
    }

    /// <summary>Arguments separated by blanks; a folder name with blanks goes in double quotes.</summary>
    internal static List<string> SplitArguments(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (any) result.Add(current.ToString());
                current.Clear();
                any = false;
            }
            else
            {
                current.Append(c);
                any = true;
            }
        }
        if (any) result.Add(current.ToString());
        return result;
    }
}
