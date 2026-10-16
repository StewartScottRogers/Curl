using Curl.Api.Callbacks;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestDebugTraceTests
{
    private static readonly DateTimeOffset Afternoon = new(2026, 10, 16, 14, 5, 6, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_RefusesANullWriterOrTimeProvider()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestDebugTrace(null!, TimeProvider.System, false, false));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestDebugTrace(new StringWriter(), null!, false, false));
    }

    [TestMethod]
    public void Trace_OfText_WritesAnInfoLineAndAnswersZero()
    {
        var error = new StringWriter();
        var trace = new LibTestDebugTrace(error, new ManualTimeProvider(Afternoon), traceTime: false, noHex: false);

        int answer = trace.Trace(CurlInfoType.Text, "Connected\n"u8);

        Assert.AreEqual(0, answer);
        Assert.AreEqual("== Info: Connected\n", error.ToString());
    }

    [TestMethod]
    public void Trace_WithTraceTime_StartsWithTheLocalTimeToTheMicrosecond()
    {
        var time = new ManualTimeProvider(Afternoon);
        time.Advance(TimeSpan.FromMicroseconds(123456));
        var error = new StringWriter();
        var trace = new LibTestDebugTrace(error, time, traceTime: true, noHex: false);

        trace.Trace(CurlInfoType.Text, "x\n"u8);
        trace.Trace(CurlInfoType.DataIn, "y"u8);

        Assert.AreEqual("14:05:06.123456 == Info: x\n14:05:06.123456 <= Recv data, 1 bytes (0x1)\n0000: 79 " + new string(' ', 45) + "y\n", error.ToString());
    }

    [TestMethod]
    [DataRow(CurlInfoType.HeaderOut, "=> Send header")]
    [DataRow(CurlInfoType.DataOut, "=> Send data")]
    [DataRow(CurlInfoType.SslDataOut, "=> Send SSL data")]
    [DataRow(CurlInfoType.HeaderIn, "<= Recv header")]
    [DataRow(CurlInfoType.DataIn, "<= Recv data")]
    [DataRow(CurlInfoType.SslDataIn, "<= Recv SSL data")]
    public void Trace_OfData_DumpsItUnderItsTitle(CurlInfoType type, string title)
    {
        var error = new StringWriter();
        var trace = new LibTestDebugTrace(error, new ManualTimeProvider(Afternoon), traceTime: false, noHex: true);

        int answer = trace.Trace(type, "ok"u8);

        Assert.AreEqual(0, answer);
        Assert.AreEqual($"{title}, 2 bytes (0x2)\n0000: ok\n", error.ToString());
    }

    [TestMethod]
    public void Trace_OfAKindUpstreamDoesNotName_WritesNothing()
    {
        var error = new StringWriter();
        var trace = new LibTestDebugTrace(error, new ManualTimeProvider(Afternoon), traceTime: true, noHex: false);

        int answer = trace.Trace((CurlInfoType)7, "x"u8);

        Assert.AreEqual(0, answer);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void DebugDump_WithHex_WritesSixteenBytesARowAndPadsTheLast()
    {
        var stream = new StringWriter();

        LibTestDebugTrace.DebugDump(string.Empty, "=> Send header", stream, [.. "GET / HTTP/1.1\r\nH"u8.ToArray(), 0x01, 0x80], noHex: false);

        Assert.AreEqual(
            "=> Send header, 19 bytes (0x13)\n"
            + "0000: 47 45 54 20 2f 20 48 54 54 50 2f 31 2e 31 0d 0a GET / HTTP/1.1..\n"
            + "0010: 48 01 80 " + new string(' ', 39) + "H..\n",
            stream.ToString());
    }

    [TestMethod]
    public void DebugDump_WithoutHex_EndsARowAtACrLf()
    {
        var stream = new StringWriter();

        LibTestDebugTrace.DebugDump("t ", "<= Recv header", stream, "ab\r\ncd\r\n\r\nx"u8, noHex: true);

        Assert.AreEqual(
            "t <= Recv header, 11 bytes (0xb)\n"
            + "0000: ab\n"
            + "0004: cd\n"
            + "0008: \n"
            + "000a: x\n",
            stream.ToString());
    }

    [TestMethod]
    public void DebugDump_WithoutHex_WritesSixtyFourBytesARow()
    {
        var stream = new StringWriter();
        byte[] data = new byte[65];
        Array.Fill(data, (byte)'a');

        LibTestDebugTrace.DebugDump(string.Empty, "<= Recv data", stream, data, noHex: true);

        Assert.AreEqual($"<= Recv data, 65 bytes (0x41)\n0000: {new string('a', 64)}\n0040: a\n", stream.ToString());
    }

    [TestMethod]
    public void DebugDump_OfNothing_WritesOnlyTheTitle()
    {
        var stream = new StringWriter();

        LibTestDebugTrace.DebugDump(string.Empty, "<= Recv data", stream, [], noHex: false);

        Assert.AreEqual("<= Recv data, 0 bytes (0x0)\n", stream.ToString());
    }

    [TestMethod]
    public void DebugDump_RefusesNullArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibTestDebugTrace.DebugDump(null!, "t", new StringWriter(), [], false));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibTestDebugTrace.DebugDump(string.Empty, null!, new StringWriter(), [], false));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibTestDebugTrace.DebugDump(string.Empty, "t", null!, [], false));
    }
}
