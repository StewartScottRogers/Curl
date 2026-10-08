using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// The limit of 5000 response headers when the CONNECT reply that opened a tunnel and HTTP/2
/// trailers count toward it (BL-1609 Notes; curl 8.21.0 measured 3000 CONNECT headers,
/// Content-Length and X-H1 to X-H1999 as 5000, the 5001st failing).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_TunnelReplyOf3000Headers_FailsWithTooLargeAtTheTransfers5001stHeader()
    {
        string accepted = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n" + ValuedHeaders("X-H", 1, 1999);
        string response = accepted + ValuedHeaders("X-H", 2000, 3000) + "\r\nok";
        MemoryStream output = new();
        MemoryStream headers = new();
        Diagnostics.Arrange("connect reply headers stored", 3000);
        Diagnostics.Arrange("scripted response", "200, Content-Length: 2, X-H1 to X-H3000, body ok");
        QueueConnector connector = new(ConnectResult.Connected(Connection(response, 65536), null, connectReplyHeadersStored: 3000));

        TransferResult result = await Handler(connector)
            .ExecuteAsync(StoredHeadersContext(output, headers, NoTransferEvents.Instance, storedBefore: 0));

        WriteResult(result);
        Diagnostics.Assert("header output length", accepted.Length, headers.Length);
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual(TooManyResponseHeaders, result.ErrorMessage);
        Assert.AreEqual(accepted, Latin1(headers.ToArray()));
        Assert.EndsWith("X-H1999: v\r\n", Latin1(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2TrailersPastThe5000thHeader_WritesTheOnesThatFitThenFailsWithTooLarge()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), .. LowercaseHeaders("x-h", 4998)]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hi"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-t1", "v"), new("x-t2", "v"), new("x-t3", "v")]), isEndStream: true, isEndHeaders: true));
        MemoryStream output = new();
        MemoryStream headerOutput = new();
        Diagnostics.Arrange("response frames", "HEADERS 200 4998 repeats of x-h, DATA hi, HEADERS trailers x-t1 to x-t3 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", output, headerOutput));

        WriteResult(result);
        string written = Latin1(headerOutput.ToArray());
        Diagnostics.Act("header output tail", OneLine(written[^Math.Min(40, written.Length)..]));
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual(TooManyResponseHeaders, result.ErrorMessage);
        Assert.EndsWith("x-h: v\r\n\r\nx-t1: v\r\nx-t2: v\r\n", written);
        Assert.AreEqual("hi", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2TrailersThatTakeTheCountTo5000_Succeed()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), .. LowercaseHeaders("x-h", 4998)]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hi"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new("x-t1", "v"), new("x-t2", "v")]), isEndStream: true, isEndHeaders: true));
        MemoryStream headerOutput = new();
        Diagnostics.Arrange("response frames", "HEADERS 200 4998 repeats of x-h, DATA hi, HEADERS trailers x-t1 x-t2 (end stream)");

        TransferResult result = await Handler(QueueConnector.For(new ScriptedConnection(response, 65536)))
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream(), headerOutput));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.EndsWith("x-t1: v\r\nx-t2: v\r\n", Latin1(headerOutput.ToArray()));
    }

    [TestMethod]
    [DataRow("", 0, 0, DisplayName = "no trailers")]
    [DataRow("a: 1\r\nb: 2\r\n", 0, 0, DisplayName = "none allowed")]
    [DataRow("a: 1\r\nb: 2\r\nc: 3\r\n", 2, 12, DisplayName = "fewer allowed than lines")]
    [DataRow("a: 1\r\nb: 2\r\n", 5, 12, DisplayName = "more allowed than lines")]
    [DataRow("a: 1\r\nb: 2", 5, 10, DisplayName = "last line without a line feed")]
    public void StoredLengthOf_GivesTheLengthOfTheFirstAllowedLines(string trailers, int allowed, int expected)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(trailers);
        Diagnostics.Arrange("trailers, allowed", $"{OneLine(trailers)}, {allowed}");

        int length = HttpProtocolHandler.StoredLengthOf(bytes, allowed);

        Diagnostics.Assert("stored length", expected, length);
        Assert.AreEqual(expected, length);
    }

    private static IEnumerable<HeaderField> LowercaseHeaders(string prefix, int count) =>
        Enumerable.Range(1, count).Select(_ => new HeaderField(prefix, "v"));
}
