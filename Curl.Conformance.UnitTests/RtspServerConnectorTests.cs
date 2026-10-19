using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="RtspServerConnector"/> and its <see cref="RtspServerConnection"/> to upstream's <c>rtspd</c>
/// at <c>curl-8_22_0</c>: each request ends at its empty line and its <c>Content-Length</c> body, is answered
/// with the part its path picks and that part's RTP packets, and the connection stays open until a reply holds
/// <c>swsclose</c> or is empty.
/// </summary>
[TestClass]
public sealed class RtspServerConnectorTests
{
    private const string Parts =
        "<reply>\n<data>\nRTSP/1.0 200 OK\nCurl-Private: swsclose\n\n</data>\n"
        + "<data1 crlf=\"headers\">\nRTSP/1.0 200 OK\nCSeq: 1\n\n</data1>\n"
        + "<data2 nonewline=\"yes\">\nlast swsclose\n</data2>\n"
        + "<servercmd>\nrtp: part 1 channel 1 size 3\nrtp: part 2 channel 0 size 2\n</servercmd>\n</reply>\n";

    [TestMethod]
    public async Task Request_IsAnsweredWithItsPartThenItsRtpPacketsAndKeptOpen()
    {
        (RtspServerConnector rtsp, IConnection connection) = await ConnectAsync(Parts);
        byte[] buffer = new byte[64];

        await connection.WriteAsync("OPTIONS rtsp://127.0.0.1:8996/5670001 RTSP/1.0\r\nCSeq: 1\r\n\r\n"u8.ToArray(), CancellationToken.None);
        int count = await connection.ReadAsync(buffer, CancellationToken.None);
        ValueTask<int> next = connection.ReadAsync(buffer, CancellationToken.None);

        // As upstream's crlf="headers" does, the RTSP status line keeps its bare line feed.
        Assert.AreEqual("RTSP/1.0 200 OK\nCSeq: 1\r\n\r\n$\u0001\u0000\u0003$_1", Encoding.Latin1.GetString(buffer, 0, count));
        Assert.IsFalse(next.IsCompleted);
        Assert.AreEqual("OPTIONS rtsp://127.0.0.1:8996/5670001 RTSP/1.0\r\nCSeq: 1\r\n\r\n", Encoding.Latin1.GetString(rtsp.ReceivedBytes.Span));
    }

    [TestMethod]
    public async Task ReplyWithSwsbounce_AnswersTheNextRequestForThatPartWithThePartAfterIt()
    {
        (_, IConnection connection) = await ConnectAsync("<reply>\n<data>\n401 swsbounce\n</data>\n<data1>\nok swsclose\n</data1>\n</reply>\n");
        byte[] buffer = new byte[64];

        await connection.WriteAsync("DESCRIBE rtsp://h/3100 RTSP/1.0\r\n\r\n"u8.ToArray(), CancellationToken.None);
        int count = await connection.ReadAsync(buffer, CancellationToken.None);
        await connection.WriteAsync("DESCRIBE rtsp://h/3100 RTSP/1.0\r\n\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("401 swsbounce\n", Encoding.Latin1.GetString(buffer, 0, count));
        Assert.AreEqual("ok swsclose\n", await ReadToEndAsync(connection));
    }

    [TestMethod]
    public async Task ReplyWithSwsclose_ClosesTheConnectionOnceRead()
    {
        (_, IConnection connection) = await ConnectAsync(Parts);

        await connection.WriteAsync("TEARDOWN rtsp://h/x5670002 RTSP/1.0\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await connection.WriteAsync("OPTIONS rtsp://h/5670001 RTSP/1.0\r\n\r\n"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("last swsclose$\u0000\u0000\u0002$_", await ReadToEndAsync(connection));
    }

    [TestMethod]
    [DataRow("OPTIONS rtsp://h/567 RTSP/1.0\r\n\r\n", "RTSP/1.0 200 OK\nCurl-Private: swsclose\n\n")]
    [DataRow("OPTIONS rtsp://h/x RTSP/1.0\r\n\r\n", "RTSP/1.0 200 OK\nCurl-Private: swsclose\n\n")]
    [DataRow("OPTIONS 5670002 RTSP/1.0\r\n\r\n", "RTSP/1.0 200 OK\nCurl-Private: swsclose\n\n")]
    [DataRow("OPTIONS\r\n\r\n", "RTSP/1.0 200 OK\nCurl-Private: swsclose\n\n")]
    [DataRow("OPTIONS rtsp://h/99999999999999999999 RTSP/1.0\r\n\r\n", "RTSP/1.0 200 OK\nCurl-Private: swsclose\n\n")]
    [DataRow("OPTIONS rtsp://h/5670009 RTSP/1.0\r\n\r\n", "")]
    public async Task Request_PicksThePartItsPathNames(string request, string reply)
    {
        (_, IConnection connection) = await ConnectAsync(Parts);

        await connection.WriteAsync(Encoding.Latin1.GetBytes(request), CancellationToken.None);

        Assert.AreEqual(reply, await ReadToEndAsync(connection));
    }

    [TestMethod]
    public async Task RequestWithABody_EndsAfterItsContentLength()
    {
        (RtspServerConnector rtsp, IConnection connection) = await ConnectAsync(Parts);
        byte[] buffer = new byte[64];

        await connection.WriteAsync("ANNOUNCE rtsp://h/567 RTSP/1.0\r\ncontent-length:  4\r\n\r\nab"u8.ToArray(), CancellationToken.None);
        ValueTask<int> read = connection.ReadAsync(buffer, CancellationToken.None);
        Assert.IsFalse(read.IsCompleted);
        await connection.WriteAsync("cd"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("RTSP/1.0 200 OK\n", Encoding.Latin1.GetString(buffer, 0, await read)[..16]);
        Assert.AreEqual("ANNOUNCE rtsp://h/567 RTSP/1.0\r\ncontent-length:  4\r\n\r\nabcd", Encoding.Latin1.GetString(rtsp.ReceivedBytes.Span));
    }

    [TestMethod]
    [DataRow("A / RTSP/1.0\r\nContent-Length: x\r\n\r\n", 35)]
    [DataRow("A / RTSP/1.0\r\nContent-Length: 2\r\n\r\nxyz", 37)]
    [DataRow("A / RTSP/1.0\r\nContent-Length: 2\r\n\r\nx", null)]
    [DataRow("A / RTSP/1.0\r\n", null)]
    public void RequestLength_EndsAtTheBodyItsHeadersGive(string received, int? length)
    {
        Assert.AreEqual(length, RtspServerConnection.RequestLength(Encoding.Latin1.GetBytes(received)));
    }

    [TestMethod]
    public async Task Connection_IsPlainLoopbackAndClosesQuietly()
    {
        (_, IConnection connection) = await ConnectAsync(Parts);

        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.IsFalse(connection.IsSecure);
        Assert.AreEqual($"127.0.0.1:{RtspServerConnector.RtspPort}", connection.RemoteEndPoint!.ToString());
        Assert.AreEqual("rtsp", RtspServerConnector.EmulatedServers.Single());
    }

    [TestMethod]
    public async Task OtherPort_ReachesTheWrappedServer()
    {
        UpstreamTestCase testCase = ParsedTestCase.From(Parts);
        RtspServerConnector rtsp = new(testCase, new NoListenPortConnector(new SwsHttpServerConnector(testCase, TimeProvider.System)));

        ConnectResult result = await rtsp.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.IsEmpty(rtsp.ReceivedDump);
    }

    private static async Task<(RtspServerConnector Rtsp, IConnection Connection)> ConnectAsync(string sections)
    {
        UpstreamTestCase testCase = ParsedTestCase.From(sections);
        RtspServerConnector rtsp = new(testCase, new SwsHttpServerConnector(testCase, TimeProvider.System));
        ConnectResult result = await rtsp.ConnectAsync(new ConnectTarget("127.0.0.1", RtspServerConnector.RtspPort, false), CancellationToken.None);
        return (rtsp, result.Connection!);
    }

    private static async Task<string> ReadToEndAsync(IConnection connection)
    {
        StringBuilder reply = new();
        byte[] buffer = new byte[4];
        int count;
        while ((count = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reply.Append(Encoding.Latin1.GetString(buffer, 0, count));
        }

        return reply.ToString();
    }
}
