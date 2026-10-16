using Curl.Api.Callbacks;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestCliTraceTests
{
    [TestMethod]
    public void Constructor_RefusesANullWriter()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestCliTrace(null!));
    }

    [TestMethod]
    public async Task Trace_OfHeaderOut_StartsEveryLineWithAnArrow()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        int answer = trace.Trace(easy, CurlInfoType.HeaderOut, "GET / HTTP/1.1\r\nHost: x\r\n\r\n"u8);

        Assert.AreEqual(0, answer);
        Assert.AreEqual("> GET / HTTP/1.1\r\n> Host: x\r\n> \r\n", output.ToString());
    }

    [TestMethod]
    public async Task Trace_WithTransferAndConnectionIds_TagsEveryLine()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Info = new TransferInfo { TransferId = 0, ConnectionId = 3 } });
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        trace.Trace(easy, CurlInfoType.Text, "Connected\n"u8);

        Assert.AreEqual("[0-3] * Connected\n", output.ToString());
    }

    [TestMethod]
    public async Task Trace_WithATransferIdAlone_TagsWithAnXForTheConnection()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Info = new TransferInfo { TransferId = 1 } });
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        trace.Trace(easy, CurlInfoType.HeaderIn, "HTTP/1.1 200 OK\r\n"u8);

        Assert.AreEqual("[1-x] < HTTP/1.1 200 OK\r\n", output.ToString());
    }

    [TestMethod]
    public async Task Trace_OfALineLeftOpen_ContinuesItWithoutANewStart()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        trace.Trace(easy, CurlInfoType.HeaderOut, "abc"u8);
        trace.Trace(easy, CurlInfoType.HeaderOut, "def\nghi\n"u8);
        trace.Trace(easy, CurlInfoType.HeaderIn, "jk"u8);
        trace.Trace(easy, CurlInfoType.HeaderIn, "l\n"u8);

        Assert.AreEqual("> abcdef\n> ghi\n< jkl\n", output.ToString());
    }

    [TestMethod]
    public async Task Trace_OfEmptyHeaderOut_WritesNothingAndClosesTheLine()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        trace.Trace(easy, CurlInfoType.Text, "open"u8);
        trace.Trace(easy, CurlInfoType.HeaderOut, []);
        trace.Trace(easy, CurlInfoType.Text, "next\n"u8);

        Assert.AreEqual("* open* next\n", output.ToString());
    }

    [TestMethod]
    [DataRow(CurlInfoType.DataIn, "{ ")]
    [DataRow(CurlInfoType.DataOut, "} ")]
    [DataRow(CurlInfoType.SslDataIn, "{ ")]
    [DataRow(CurlInfoType.SslDataOut, "} ")]
    public async Task Trace_OfARunOfData_WritesOneBytesLineUntilOtherTraceComes(CurlInfoType type, string start)
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        trace.Trace(easy, type, new byte[100]);
        trace.Trace(easy, type, new byte[50]);
        trace.Trace(easy, CurlInfoType.Text, "x\n"u8);
        trace.Trace(easy, type, new byte[5]);

        Assert.AreEqual($"{start}[100 bytes data]\n* x\n{start}[5 bytes data]\n", output.ToString());
    }

    [TestMethod]
    public async Task Trace_OfAKindUpstreamDoesNotName_ClosesTheLineAndStopsDataLines()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);

        trace.Trace(easy, CurlInfoType.HeaderOut, "abc"u8);
        int answer = trace.Trace(easy, (CurlInfoType)7, "ignored"u8);
        trace.Trace(easy, CurlInfoType.DataIn, new byte[10]);
        trace.Trace(easy, CurlInfoType.Text, "x\n"u8);

        Assert.AreEqual(0, answer);
        Assert.AreEqual("> abc* x\n", output.ToString());
    }

    [TestMethod]
    public async Task CallbackFor_TracesForTheHandle()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Info = new TransferInfo { TransferId = 2, ConnectionId = 0 } });
        await using CurlEasy easy = library.EasyInit();
        var output = new StringWriter();
        var trace = new LibTestCliTrace(output);
        DebugCallback callback = trace.CallbackFor(easy);

        int answer = callback(CurlInfoType.Text, "hi\n"u8);

        Assert.AreEqual(0, answer);
        Assert.AreEqual("[2-0] * hi\n", output.ToString());
    }

    [TestMethod]
    public void Trace_AndCallbackFor_RefuseANullHandle()
    {
        var trace = new LibTestCliTrace(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => trace.CallbackFor(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => trace.Trace(null!, CurlInfoType.Text, []));
    }
}
