using System.Text;

namespace TextTools.Core.Tests;

/// <summary>
/// Every command gets hostile input. Allowed outcomes: a result, or FormatException (shown in the status bar).
/// Anything else would surface as an error dialog in the IDE.
/// </summary>
public class RobustnessTests
{
    private static readonly string[] Nasty =
    [
        "", " ", "\r\n", "\n\n\n", "\t", "'", "''", "'''", "\"", "q'", "q'[", "q'[]'", "Q'{x}'", "-", "+", ".", ",", ":", "=>", ":=",
        "0", "-0", "9223372036854775807", "-9223372036854775808", "99999999999999999999999999999999",
        "-1", "1e10", "NaN", "P", "PT", "-P", "PT-1H", "P99999999999999999999Y", "PT99999999999999999999999999999H",
        "PT9999999999999999S", "P9999999999999999999999W", "PT0.0000000001S", "P1Y2M3DT4H5M6S",
        "0000-00-00", "9999-12-31 23:59:59.999999999 +14:00", "0001-01-01T00:00:00Z", "2026-02-29", "29.02.2024",
        "1/1/1", "13/13/2026", "99/99/9999", "2026-10-13 25:61:61", "2026-10-13 +99:99", "TO_DATE(", "DATE ''", "TIMESTAMP '",
        "yyyy", "YYYY", "'yyyy", "\"yyyy", "\"\"", "FF10", "fffffffffff", "SSSSSSSSSSSS", "TZH:", "A.M", "\"unclosed",
        "привет_мир", "ПриветМир", "𝔘𝔫𝔦𝔠𝔬𝔡𝔢", "😀😀😀", "\uD800", "\uDC00x", "\0", "a\0b", "%", "%zz", "%E0%A4%A",
        "====", "SGVsbG8", "zz", "0x", "0xZZ", "abc", "�",
        new string('a', 100_000), new string('\n', 10_000), string.Join("\n", Enumerable.Range(0, 5000)),
        string.Join("\r\n", Enumerable.Repeat("  l_x := 1;", 2000)), new string('_', 5000), new string('-', 5000),
    ];

    public static IEnumerable<object[]> Cases() =>
        Commands.All.OfType<TransformCommand>().Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(Cases))]
    public void NeverThrowsAnythingButFormatException(string name)
    {
        var cmd = Commands.All.OfType<TransformCommand>().Single(c => string.Equals(c.Name, name, StringComparison.Ordinal));
        foreach (var input in Nasty.Concat(RandomInputs(300)))
        {
            try
            {
                Assert.NotNull(cmd.Transform(input));
            }
            catch (FormatException)
            {
                // expected for invalid input: shown in the status bar
            }
            catch (Exception e)
            {
                Assert.Fail($"{name} on {Describe(input)}: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    [Fact]
    public void InsertCommandsHandleExtremeMoments()
    {
        DateTimeOffset[] moments = [DateTimeOffset.MinValue, DateTimeOffset.MaxValue, DateTimeOffset.UnixEpoch,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(14)), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-12))];
        foreach (var cmd in Commands.All.OfType<InsertCommand>())
        {
            foreach (var m in moments)
            {
                Assert.False(string.IsNullOrEmpty(cmd.Format(m)), cmd.Name);
            }
        }
    }

    [Fact]
    public void CopyCommandsNeverThrowAnythingButFormatException()
    {
        var rnd = new Random(3);
        string[] plsql =
        [
            "declare c constant number := 1; begin select c into l from t; end;", "package body p is procedure x is begin",
            "create package p as c constant varchar2(1) := 'x'", "begin update t set a = p.c returning a into l; end;",
            "end end end; end if; end loop; end case;", "select (((( from", "select )))) from", "q'[", "function f(", "procedure",
        ];
        foreach (var cmd in Commands.All.OfType<CopyCommand>())
        {
            foreach (var text in Nasty.Concat(plsql).Concat(RandomInputs(300)).Concat(plsql.Select(p => string.Concat(Enumerable.Repeat(p, 50)))))
            {
                for (var i = 0; i < 5; i++)
                {
                    var start = rnd.Next(-2, text.Length + 2);
                    var request = new Sql.SqlRequest(text, start, i % 2 == 0 ? 0 : rnd.Next(-1, 20), rnd.Next(-2, text.Length + 2));
                    try
                    {
                        Assert.NotNull(cmd.Build(request, null).Text);
                    }
                    catch (FormatException)
                    {
                        // expected: no statement at the cursor, selection outside the text
                    }
                    catch (Exception e)
                    {
                        Assert.Fail($"{cmd.Name} on {Describe(text)} at {start}: {e.GetType().Name}: {e.Message}");
                    }
                }
            }
        }
    }

    [Fact]
    public void LocatorNeverThrows()
    {
        var rnd = new Random(1);
        foreach (var text in Nasty.Take(60))
        {
            for (var i = 0; i < 50; i++)
            {
                int x = rnd.Next(-5, 50), y = rnd.Next(-5, 50);
                var sel = text.Length == 0 ? "a" : text.Substring(rnd.Next(text.Length), 1);
                SelectionLocator.Replace(text, sel, x, y, s => s + s);
                SelectionLocator.Insert(text, x, y, "X");
            }
        }
    }

    private static IEnumerable<string> RandomInputs(int count)
    {
        var rnd = new Random(42);
        const string alphabet = "0123456789-+.:/,'\"TZPYMWDHSF qQ[]{}()_$#\r\nAaBbYyMmDdHhSsfFtTzZxXEeKkЖж😀";
        for (var i = 0; i < count; i++)
        {
            var sb = new StringBuilder();
            var len = rnd.Next(1, 40);
            for (var j = 0; j < len; j++) sb.Append(alphabet[rnd.Next(alphabet.Length)]);
            yield return sb.ToString();
        }
    }

    private static string Describe(string s) => s.Length > 40 ? $"<{s.Length} chars: {s[..20]}...>" : $"'{s}'";
}
