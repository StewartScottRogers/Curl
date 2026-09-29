using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="DiagnosticLogWriter"/> to the line format and rules of ADR-0222,
/// decisions 2, 3 and 5.
/// </summary>
[TestClass]
public sealed class DiagnosticLogWriterTests
{
    private const string LineEnd = "\r\n";

    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 9, 29, 14, 3, 7, 123, TimeSpan.Zero));

    private readonly StringWriter output = new();

    [TestMethod]
    public void Write_Info_WritesTimestampLevelComponentMessageAndLineEnd()
    {
        DiagnosticLogWriter log = new(output, DiagnosticLogLevel.Verbose, Clock, LineEnd);

        log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Http, "reply 200");

        Assert.AreEqual("[2026-09-29T14:03:07.123Z] [info] [http] reply 200\r\n", output.ToString());
    }

    [TestMethod]
    public void Write_EachLevel_WritesItsLowerCaseName()
    {
        DiagnosticLogWriter log = new(output, DiagnosticLogLevel.Verbose, Clock, "\n");

        log.Write(DiagnosticLogLevel.Error, "cli", "e");
        log.Write(DiagnosticLogLevel.Warning, "cli", "w");
        log.Write(DiagnosticLogLevel.Info, "cli", "i");
        log.Write(DiagnosticLogLevel.Verbose, "cli", "v");

        Assert.AreEqual(
            "[2026-09-29T14:03:07.123Z] [error] [cli] e\n"
            + "[2026-09-29T14:03:07.123Z] [warning] [cli] w\n"
            + "[2026-09-29T14:03:07.123Z] [info] [cli] i\n"
            + "[2026-09-29T14:03:07.123Z] [verbose] [cli] v\n",
            output.ToString());
    }

    [TestMethod]
    public void Write_TimestampWithNonZeroOffset_IsWrittenInUtc()
    {
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 9, 29, 7, 3, 7, 5, TimeSpan.FromHours(-7)));
        DiagnosticLogWriter log = new(output, DiagnosticLogLevel.Info, clock, "\n");

        log.Write(DiagnosticLogLevel.Info, "dns", "resolved");

        Assert.AreEqual("[2026-09-29T14:03:07.005Z] [info] [dns] resolved\n", output.ToString());
    }

    // Each string is IsEnabled for None, Error, Warning, Info and Verbose, in that order.
    [TestMethod]
    [DataRow(DiagnosticLogLevel.Error, "01000")]
    [DataRow(DiagnosticLogLevel.Warning, "01100")]
    [DataRow(DiagnosticLogLevel.Info, "01110")]
    [DataRow(DiagnosticLogLevel.Verbose, "01111")]
    public void IsEnabled_EachLevelAtEachConfiguredLevel_IsCumulative(DiagnosticLogLevel configured, string expected)
    {
        DiagnosticLogWriter log = new(output, configured, Clock, LineEnd);

        string actual = string.Concat(
            Enum.GetValues<DiagnosticLogLevel>().Select(level => log.IsEnabled(level) ? '1' : '0'));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(DiagnosticLogLevel.None)]
    [DataRow(DiagnosticLogLevel.Info)]
    [DataRow(DiagnosticLogLevel.Verbose)]
    [DataRow((DiagnosticLogLevel)(-1))]
    public void Write_DisabledLevel_WritesNothing(DiagnosticLogLevel level)
    {
        DiagnosticLogWriter log = new(output, DiagnosticLogLevel.Warning, Clock, LineEnd);

        log.Write(level, "http", "ignored");

        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public void Write_MessageWithCarriageReturnAndLineFeed_EscapesThemOnOneLine()
    {
        DiagnosticLogWriter log = new(output, DiagnosticLogLevel.Info, Clock, "\n");

        log.Write(DiagnosticLogLevel.Info, "http", "a\rb\nc\r\nd");

        Assert.AreEqual("[2026-09-29T14:03:07.123Z] [info] [http] a\\rb\\nc\\r\\nd\n", output.ToString());
    }

    [TestMethod]
    [DataRow(DiagnosticLogLevel.None)]
    [DataRow((DiagnosticLogLevel)5)]
    [DataRow((DiagnosticLogLevel)(-1))]
    public void Constructor_LevelNoneOrUndefined_Throws(DiagnosticLogLevel level)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DiagnosticLogWriter(output, level, Clock, LineEnd));
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiagnosticLogWriter(null!, DiagnosticLogLevel.Info, Clock, LineEnd));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiagnosticLogWriter(output, DiagnosticLogLevel.Info, null!, LineEnd));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiagnosticLogWriter(output, DiagnosticLogLevel.Info, Clock, null!));
    }

    [TestMethod]
    public void Write_TargetThrowsIOException_DoesNotThrowAndSkipsLaterLines()
    {
        FailingWriter target = new();
        DiagnosticLogWriter log = new(target, DiagnosticLogLevel.Info, Clock, LineEnd);

        log.Write(DiagnosticLogLevel.Info, "http", "first");
        log.Write(DiagnosticLogLevel.Info, "http", "second");

        Assert.AreEqual(1, target.WriteCalls);
    }

    [TestMethod]
    public void Write_EachLine_FlushesTheTarget()
    {
        FlushCountingWriter target = new();
        DiagnosticLogWriter log = new(target, DiagnosticLogLevel.Info, Clock, LineEnd);

        log.Write(DiagnosticLogLevel.Info, "http", "one");
        log.Write(DiagnosticLogLevel.Error, "http", "two");

        Assert.AreEqual(2, target.Flushes);
    }

    [TestMethod]
    public async Task Write_TwoThreadsThousandLinesEach_WritesWholeLines()
    {
        DiagnosticLogWriter log = new(output, DiagnosticLogLevel.Verbose, Clock, "\n");

        await Task.WhenAll(
            Task.Run(() => WriteLines(log, "one")),
            Task.Run(() => WriteLines(log, "two")));

        string[] lines = output.ToString().Split('\n');
        Assert.AreEqual(2001, lines.Length);
        Assert.AreEqual(string.Empty, lines[^1]);
        foreach (string line in lines[..^1])
        {
            StringAssert.Matches(line, new System.Text.RegularExpressions.Regex(@"^\[2026-09-29T14:03:07\.123Z\] \[verbose\] \[(one|two)\] line \d{1,3}$"));
        }

        Assert.AreEqual(1000, lines.Count(line => line.Contains("[one]", StringComparison.Ordinal)));
    }

    private static void WriteLines(DiagnosticLogWriter log, string component)
    {
        for (int index = 0; index < 1000; index++)
        {
            log.Write(DiagnosticLogLevel.Verbose, component, $"line {index}");
        }
    }

    private sealed class FailingWriter : StringWriter
    {
        public int WriteCalls { get; private set; }

        public override void Write(string? value)
        {
            WriteCalls++;
            throw new IOException("The disk is full.");
        }
    }

    private sealed class FlushCountingWriter : StringWriter
    {
        public int Flushes { get; private set; }

        public override void Flush()
        {
            Flushes++;
        }
    }
}
