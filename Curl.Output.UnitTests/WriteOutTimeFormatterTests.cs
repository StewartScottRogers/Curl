using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="WriteOutTimeFormatter"/>'s <see cref="WriteOutTimeDialect.WindowsCRuntime"/> dialect to what curl 8.21.0 (mingw, Schannel) wrote for
/// <c>-w "[%time{…}]"</c> on 2026-09-26; the commands are in BL-279's Notes.
/// </summary>
[TestClass]
public sealed class WriteOutTimeFormatterTests
{
    // Sunday 2026-09-27 03:30:08.545957 UTC, the instant of the reference measurements.
    private static readonly FixedTimeProvider Measured = new(new DateTimeOffset(2026, 9, 27, 3, 30, 8, TimeSpan.Zero).AddTicks(5_459_570));

    // Sunday 2005-01-02 15:04:05.000007 UTC, where every number has a leading zero to drop.
    private static readonly FixedTimeProvider LeadingZeros = new(new DateTimeOffset(2005, 1, 2, 15, 4, 5, TimeSpan.Zero).AddTicks(70));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("%a|%A|%b|%B", "Sun|Sunday|Sep|September")]
    [DataRow("%c", "9/27/2026 3:30:08 AM")]
    [DataRow("%x|%X", "9/27/2026|3:30:08 AM")]
    [DataRow("%d|%H|%I|%j|%m|%M|%S|%p", "27|03|03|270|09|30|08|AM")]
    [DataRow("%U|%w|%W|%y|%Y", "39|0|38|26|2026")]
    [DataRow("%z|%Z|%%|%f|%s", "+0000|UTC|%|545957|1790479808")]
    [DataRow("%%f", "%f")]
    [DataRow("abc", "abc")]
    [DataRow("", "")]
    public void Format_MeasuredConversions_MatchCurl(string format, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string actual = RunFormat(diagnostics, format, expected, Measured);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("%#c", "Sunday, September 27, 2026 3:30:08 AM")]
    [DataRow("%#x|%#X", "Sunday, September 27, 2026|3:30:08 AM")]
    [DataRow("%#a|%#A|%#b|%#B|%#p|%#%", "Sun|Sunday|Sep|September|AM|%")]
    [DataRow("%#d|%#H|%#I|%#j|%#m|%#M|%#S|%#U|%#w|%#W|%#y|%#Y", "27|3|3|270|9|30|8|39|0|38|26|2026")]
    [DataRow("%#z|%#Z", FixedTimeProvider.TimeZoneStandardName + "|" + FixedTimeProvider.TimeZoneStandardName)]
    public void Format_AlternateConversions_MatchCurl(string format, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string actual = RunFormat(diagnostics, format, expected, Measured);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("%d|%H|%I|%j|%m|%M|%S|%U|%W|%y|%p|%f", "02|15|03|002|01|04|05|01|00|05|PM|000007")]
    [DataRow("%#d|%#H|%#I|%#j|%#m|%#M|%#S|%#U|%#W|%#y", "2|15|3|2|1|4|5|1|0|5")]
    public void Format_LeadingZeros_AreDroppedOnlyByTheAlternateFlag(string format, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string actual = RunFormat(diagnostics, format, expected, LeadingZeros);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("abc%Qdef")]
    [DataRow("%C")]
    [DataRow("%e")]
    [DataRow("%n%t")]
    [DataRow("%Ey")]
    [DataRow("%#f")]
    [DataRow("%#s")]
    [DataRow("%#Q")]
    [DataRow("%Y%")]
    [DataRow("x%#")]
    public void Format_ConversionTheRuntimeRejects_RendersNothing(string format)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string actual = RunFormat(diagnostics, format, string.Empty, Measured);

        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    public void Format_ResultOf255Bytes_Fits()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string format = new('a', 255);

        string actual = RunFormat(diagnostics, format, format, Measured);

        Assert.AreEqual(format, actual);
    }

    [TestMethod]
    public void Format_ResultOf256Bytes_RendersNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string format = new('a', 256);

        string actual = RunFormat(diagnostics, format, string.Empty, Measured);

        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    public void Format_NullArgument_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("arguments", "null format, then null time provider");

        ArgumentNullException first = Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTimeFormatter.Format(null!, WriteOutTimeDialect.WindowsCRuntime, Measured));
        ArgumentNullException second = Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTimeFormatter.Format("%Y", WriteOutTimeDialect.WindowsCRuntime, null!));

        diagnostics.Act("null format throws", first.GetType().Name + ": " + first.Message);
        diagnostics.Act("null time provider throws", second.GetType().Name + ": " + second.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), first.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), second.GetType().Name);
    }

    [TestMethod]
    public void Format_UndefinedDialect_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("dialect", 2);

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WriteOutTimeFormatter.Format("%Y", (WriteOutTimeDialect)2, Measured));

        diagnostics.Act("throws", exception.GetType().Name + ": " + exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    private static string RunFormat(TestDiagnostics diagnostics, string format, string expected, FixedTimeProvider clock)
    {
        diagnostics.Arrange("format", format);

        string actual = WriteOutTimeFormatter.Format(format, WriteOutTimeDialect.WindowsCRuntime, clock);

        diagnostics.Act("rendered", actual);
        diagnostics.Diff("rendered", expected, actual);
        return actual;
    }
}
