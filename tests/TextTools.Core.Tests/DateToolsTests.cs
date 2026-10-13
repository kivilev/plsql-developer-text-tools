namespace TextTools.Core.Tests;

public class NowTests
{
    // 2026-10-13 14:30:05.123456 -03:00
    private static readonly DateTimeOffset M = new DateTimeOffset(2026, 10, 13, 14, 30, 5, TimeSpan.FromHours(-3)).AddTicks(1_234_560);

    [Fact] public void Date() => Assert.Equal("DATE '2026-10-13'", DateText.NowDate(M));
    [Fact] public void Timestamp() => Assert.Equal("TIMESTAMP '2026-10-13 14:30:05'", DateText.NowTimestamp(M));
    [Fact] public void TimestampFraction() => Assert.Equal("TIMESTAMP '2026-10-13 14:30:05.123456'", DateText.NowTimestampFraction(M));
    [Fact] public void TimestampTz() => Assert.Equal("TIMESTAMP '2026-10-13 14:30:05.123456 -03:00'", DateText.NowTimestampTz(M));
    [Fact] public void ToDate() => Assert.Equal("TO_DATE('2026-10-13 14:30:05', 'YYYY-MM-DD HH24:MI:SS')", DateText.NowToDate(M));
    [Fact] public void Iso() => Assert.Equal("2026-10-13T14:30:05-03:00", DateText.NowIso(M));
    [Fact] public void Epoch() => Assert.Equal(M.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), DateText.NowEpoch(M));

    [Fact]
    public void InsertAtCursor()
    {
        var r = SelectionLocator.Insert("ab\r\ncd", 2, 2, "X")!.Value;
        Assert.Equal("ab\r\ncXd", r.Text);
        Assert.Equal((3, 2), (r.CursorX, r.CursorY));
        Assert.Equal("X", SelectionLocator.Insert("", 1, 1, "X")!.Value.Text);
    }
}

public class DateConvertTests
{
    [Theory]
    [InlineData("1760356800", "TIMESTAMP '2025-10-13 12:00:00'")]
    [InlineData("1760356800123", "TIMESTAMP '2025-10-13 12:00:00.123'")]
    [InlineData("1760356800123456", "TIMESTAMP '2025-10-13 12:00:00.123456'")]
    [InlineData("-1", "TIMESTAMP '1969-12-31 23:59:59'")]
    public void EpochToTimestamp(string input, string expected) => Assert.Equal(expected, DateText.EpochToTimestamp(input));

    [Theory]
    [InlineData("2025-10-13 12:00:00")]
    [InlineData("2025-10-13T15:00:00+03:00")]
    [InlineData("2025-10-13T12:00:00Z")]
    [InlineData("13.10.2025 12:00")]
    [InlineData("TIMESTAMP '2025-10-13 12:00:00'")]
    public void ToEpoch(string input) => Assert.Equal("1760356800", DateText.ToEpoch(input));

    [Theory]
    [InlineData("13.10.2026", "TO_DATE('2026-10-13', 'YYYY-MM-DD')")]
    [InlineData("2026-10-13 14:30", "TO_DATE('2026-10-13 14:30:00', 'YYYY-MM-DD HH24:MI:SS')")]
    [InlineData("2026-10-13 14:30:00,5", "TO_TIMESTAMP('2026-10-13 14:30:00.5', 'YYYY-MM-DD HH24:MI:SS.FF')")]
    [InlineData("2026-10-13T14:30:00.123Z", "TO_TIMESTAMP_TZ('2026-10-13 14:30:00.123 +00:00', 'YYYY-MM-DD HH24:MI:SS.FF TZH:TZM')")]
    public void ToDateCall(string input, string expected) => Assert.Equal(expected, DateText.ToDateCall(input));

    [Theory]
    [InlineData("'2026-10-13'", "DATE '2026-10-13'")]
    [InlineData("TO_DATE('13.10.2026 14:30', 'DD.MM.YYYY HH24:MI')", "TIMESTAMP '2026-10-13 14:30:00'")]
    [InlineData("10/13/2026", "DATE '2026-10-13'")]
    [InlineData("13/10/2026", "DATE '2026-10-13'")]
    [InlineData("2026-10-13 14:30:00 +0530", "TIMESTAMP '2026-10-13 14:30:00 +05:30'")]
    public void ToLiteral(string input, string expected) => Assert.Equal(expected, DateText.ToLiteral(input));

    [Fact]
    public void PerLineKeepsIndentAndCommas() =>
        Assert.Equal("  DATE '2026-10-13',\r\n  DATE '2026-10-14'", DateText.ToLiteral("  13.10.2026,\r\n  14.10.2026"));

    [Theory]
    [InlineData("01/02/2026")]   // day/month order unknown
    [InlineData("31.02.2026")]   // no such day
    [InlineData("yesterday")]
    public void BadDatesAreRejected(string input) => Assert.Throws<FormatException>(() => DateText.ToLiteral(input));
}

public class DateMaskTests
{
    [Theory]
    [InlineData("yyyy-MM-dd'T'HH:mm:ss.SSSXXX", "YYYY-MM-DD\"T\"HH24:MI:SS.FF3TZH:TZM")]
    [InlineData("\"dd.MM.yyyy HH:mm\"", "'DD.MM.YYYY HH24:MI'")]
    [InlineData("yyyy-MM-dd hh:mm:ss.fff tt zzz", "YYYY-MM-DD HH12:MI:SS.FF3 AM TZH:TZM")]
    [InlineData("EEE, d MMM yyyy", "Dy, DD Mon YYYY")]
    public void ToOracle(string input, string expected) => Assert.Equal(expected, DateMasks.ToOracle(input));

    [Theory]
    [InlineData("YYYY-MM-DD HH24:MI:SS", "yyyy-MM-dd HH:mm:ss")]
    [InlineData("'DD.MM.YYYY \"at\" HH24:MI'", "\"dd.MM.yyyy 'at' HH:mm\"")]
    [InlineData("fmDD Month YYYY", "dd MMMM yyyy")]
    public void ToJava(string input, string expected) => Assert.Equal(expected, DateMasks.ToJava(input));

    [Fact]
    public void ToDotNet() =>
        Assert.Equal("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateMasks.ToDotNet("YYYY-MM-DD\"T\"HH24:MI:SS.FF3TZH:TZM"));

    [Fact]
    public void RoundTrip() => Assert.Equal("yyyy-MM-dd HH:mm:ss", DateMasks.ToJava(DateMasks.ToOracle("yyyy-MM-dd HH:mm:ss")));

    [Fact]
    public void UnknownLetterIsRejected() => Assert.Throws<FormatException>(() => DateMasks.ToOracle("yyyy-QQ"));
}

public class IntervalTests
{
    [Theory]
    [InlineData("PT1H30M", "INTERVAL '1:30' HOUR TO MINUTE")]
    [InlineData("P2DT10H", "INTERVAL '2 10' DAY TO HOUR")]
    [InlineData("P2DT10H30M", "INTERVAL '2 10:30' DAY TO MINUTE")]
    [InlineData("PT90M", "INTERVAL '90' MINUTE")]
    [InlineData("PT1H90M", "INTERVAL '2:30' HOUR TO MINUTE")]
    [InlineData("PT150M", "INTERVAL '150' MINUTE(3)")]
    [InlineData("P1W", "INTERVAL '7' DAY")]
    [InlineData("-PT1M5S", "INTERVAL '-1:05' MINUTE TO SECOND")]
    [InlineData("PT1M30.5S", "INTERVAL '1:30.5' MINUTE TO SECOND")]
    [InlineData("PT0S", "INTERVAL '0' SECOND")]
    [InlineData("P1Y2M", "INTERVAL '1-2' YEAR TO MONTH")]
    [InlineData("P14M", "INTERVAL '1-2' YEAR TO MONTH")]
    [InlineData("P3M", "INTERVAL '3' MONTH")]
    public void FromIsoDuration(string input, string expected) => Assert.Equal(expected, Interval.FromIsoDuration(input));

    [Theory]
    [InlineData("P1YT1H")]  // Oracle cannot mix YEAR TO MONTH with DAY TO SECOND
    [InlineData("PT")]
    [InlineData("P")]
    [InlineData("1H30M")]
    public void Rejects(string input) => Assert.Throws<FormatException>(() => Interval.FromIsoDuration(input));
}
