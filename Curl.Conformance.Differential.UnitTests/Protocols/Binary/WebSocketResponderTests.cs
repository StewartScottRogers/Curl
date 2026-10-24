using System.Text;
using Curl.Conformance.Differential.Protocols.Binary;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Binary;

[TestClass]
public sealed class WebSocketResponderTests
{
    private const string Request = "GET / HTTP/1.1\r\nHost: x\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n";
    private const string RequestLog = "GET / HTTP/1.1\nHost: x\n\n\n";
    private const string Reply = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n"
        + "\u0081\u0005hello\u0082\u0003\u0001\u0002\u0003\u0088\u0000";

    private static readonly byte[] Mask = [1, 2, 3, 4];

    [TestMethod]
    public async Task RespondAsync_MaskedFramesThenClose_AnswersTheUpgradeAndLogsEachFrame()
    {
        byte[] input = [.. Bytes(Request), .. MaskedFrame(1, "hi"u8), .. MaskedFrame(8, []), .. MaskedFrame(1, "late"u8)];

        var (output, log) = await RunAsync(input);

        Assert.AreEqual(Reply, output);
        Assert.AreEqual(RequestLog + "\nframe op=1 masked=True len=2 data=b'hi'\nframe op=8 masked=True len=0 data=b''", log);
    }

    [TestMethod]
    public async Task RespondAsync_LongFrame_ReadsTheExtendedLengthAndLogsTheFirstTwentyBytes()
    {
        var data = Enumerable.Repeat((byte)'a', 130).ToArray();
        byte[] frame = [0x81, 0xFE, 0, 130, .. Mask, .. data.Select((b, i) => (byte)(b ^ Mask[i % 4]))];

        var (_, log) = await RunAsync([.. Bytes(Request), .. frame]);

        Assert.AreEqual(RequestLog + "\nframe op=1 masked=True len=130 data=b'" + new string('a', 20) + "'", log);
    }

    [TestMethod]
    public async Task RespondAsync_UnmaskedEmptyFrame_LogsItAsNotMasked()
    {
        var (_, log) = await RunAsync([.. Bytes(Request), 0x81, 0x00]);

        Assert.AreEqual(RequestLog + "\nframe op=1 masked=False len=0 data=b''", log);
    }

    [TestMethod]
    public async Task RespondAsync_ShortMaskWithoutData_LogsTheEmptyFrame()
    {
        var (_, log) = await RunAsync([.. Bytes(Request), 0x81, 0x80, 1, 2]);

        Assert.AreEqual(RequestLog + "\nframe op=1 masked=True len=0 data=b''", log);
    }

    [TestMethod]
    public async Task RespondAsync_UnmaskedFrameWithData_EndsWithoutLoggingIt()
    {
        var (_, log) = await RunAsync([.. Bytes(Request), 0x81, 0x02, (byte)'h', (byte)'i', .. MaskedFrame(1, "hi"u8)]);

        Assert.AreEqual(RequestLog, log);
    }

    [TestMethod]
    public async Task RespondAsync_ShortExtendedLength_EndsWithoutLoggingIt()
    {
        var (_, log) = await RunAsync([.. Bytes(Request), 0x81, 0xFE, 0]);

        Assert.AreEqual(RequestLog, log);
    }

    [TestMethod]
    public async Task RespondAsync_OneByteOfFrameHeader_EndsWithoutLoggingIt()
    {
        var (_, log) = await RunAsync([.. Bytes(Request), 0x81]);

        Assert.AreEqual(RequestLog, log);
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\nHost: x\r\n\r\n", "GET / HTTP/1.1\nHost: x\n\n\n")]
    [DataRow("GET / HTTP/1.1\r\nSec-WebSocket-Key\r\n\r\n", "GET / HTTP/1.1\n\n\n")]
    [DataRow("GET / HTTP/1.1\r\n", "GET / HTTP/1.1\n\n")]
    public async Task RespondAsync_NoUsableKey_LogsTheRequestAndSendsNothing(string request, string expectedLog)
    {
        var (output, log) = await RunAsync(Bytes(request));

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(expectedLog, log);
    }

    [TestMethod]
    public async Task RespondAsync_KeyWithLowerCaseHeaderAndPadding_AnswersTheSameAccept()
    {
        var (output, _) = await RunAsync(Bytes("GET / HTTP/1.1\r\nsec-websocket-key:\t dGhlIHNhbXBsZSBub25jZQ== \r\n\r\n"));

        Assert.AreEqual(Reply, output);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static byte[] MaskedFrame(int opcode, ReadOnlySpan<byte> data) =>
        [(byte)(0x80 | opcode), (byte)(0x80 | data.Length), .. Mask, .. data.ToArray().Select((b, i) => (byte)(b ^ Mask[i % 4]))];

    private static async Task<(string Output, string Log)> RunAsync(byte[] input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(input);

        await new WebSocketResponder(log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
