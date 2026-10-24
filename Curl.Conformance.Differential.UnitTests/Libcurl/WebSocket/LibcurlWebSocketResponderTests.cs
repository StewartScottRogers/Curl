using System.Text;
using Curl.Conformance.Differential.Libcurl.WebSocket;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.WebSocket;

[TestClass]
public sealed class LibcurlWebSocketResponderTests
{
    private const string Request = "GET /ws HTTP/1.1\r\nHost: x\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n";
    private const string RequestLog = "GET /ws HTTP/1.1\r\nHost: x\r\n\r\n";
    private const string Reply = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n";

    private static readonly byte[] Mask = [1, 2, 3, 4];

    [TestMethod]
    public async Task RespondAsync_ServerHangsUp_SendsTheFramesAndReadsNoClientFrame()
    {
        var (output, log) = await RunAsync(Case([0x81, 1, (byte)'a'], hangsUp: true), [.. Bytes(Request), .. MaskedFrame(0x81, "late"u8)]);

        Assert.AreEqual(Reply + "\u0081\u0001a", output);
        Assert.AreEqual(RequestLog, log);
    }

    [TestMethod]
    public async Task RespondAsync_PingTextAndClose_AnswersPingAndCloseThenStops()
    {
        byte[] input = [.. Bytes(Request), .. MaskedFrame(0x89, "p"u8), .. MaskedFrame(0x01, "ab"u8), .. MaskedFrame(0x88, [3, 232]), .. MaskedFrame(0x81, "late"u8)];

        var (output, log) = await RunAsync(Case([]), input);

        Assert.AreEqual(Reply + "\u008A\u0001p\u0088\u0002\u0003è", output);
        Assert.AreEqual(
            RequestLog + "\nframe fin=True op=9 masked=True len=1 data=b'p'\nframe fin=False op=1 masked=True len=2 data=b'ab'\nframe fin=True op=8 masked=True len=2 data=b'\\x03\\xe8'",
            log);
    }

    [TestMethod]
    public async Task RespondAsync_ExtendedLengthUnmaskedFrame_LogsItsWholePayload()
    {
        byte[] input = [.. Bytes(Request), 0x82, 126, 0, 130, .. new byte[130]];

        var (_, log) = await RunAsync(Case([]), input);

        StringAssert.EndsWith(log, "frame fin=True op=2 masked=False len=130 data=b'" + string.Concat(Enumerable.Repeat("\\x00", 130)) + "'");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81 }, DisplayName = "short header")]
    [DataRow(new byte[] { 0x81, 126, 0 }, DisplayName = "short extended length")]
    [DataRow(new byte[] { 0x81, 0x81, 1, 2 }, DisplayName = "short mask")]
    [DataRow(new byte[] { 0x81, 3, (byte)'a' }, DisplayName = "short payload")]
    public async Task RespondAsync_CutShortFrame_StopsWithoutLoggingIt(byte[] frame)
    {
        var (_, log) = await RunAsync(Case([]), [.. Bytes(Request), .. frame]);

        Assert.AreEqual(RequestLog, log);
    }

    [TestMethod]
    public async Task RespondAsync_RequestEndsBeforeItsBlankLine_StillAnswers()
    {
        var (output, log) = await RunAsync(Case([]), Bytes("GET / HTTP/1.1\r\n"));

        Assert.AreEqual(RequestLog.Replace("/ws", "/", StringComparison.Ordinal).Replace("Host: x\r\n\r\n", "", StringComparison.Ordinal), log);
        StringAssert.StartsWith(output, "HTTP/1.1 101 Switching Protocols\r\n");
    }

    [TestMethod]
    public async Task RespondAsync_NullConnection_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new LibcurlWebSocketResponder(Case([]), new ProtocolWireLog()).RespondAsync(null!, CancellationToken.None));

    private static LibcurlWebSocketCase Case(byte[] serverFrames, bool hangsUp = false) => new("test", serverFrames, hangsUp, true, []);

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static byte[] MaskedFrame(int first, ReadOnlySpan<byte> data) =>
        [(byte)first, (byte)(0x80 | data.Length), .. Mask, .. data.ToArray().Select((b, i) => (byte)(b ^ Mask[i % 4]))];

    private static async Task<(string Output, string Log)> RunAsync(LibcurlWebSocketCase webSocketCase, byte[] input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(input);

        await new LibcurlWebSocketResponder(webSocketCase, log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
