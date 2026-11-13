using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the resend of a stream-bodied upload (<c>-T file</c>, <c>-F name=@file</c>) on a fresh
/// connection after the reused one died before the response: curl rewinds a seekable body and
/// sends it again, and a body that cannot seek is not sent again (BL-5130, STD-053).
/// </summary>
[TestClass]
public sealed class HttpRewindableUploadResendTests
{
    private const string Url = "http://127.0.0.1:18977/echo/b";

    private const string KeepAlive200 = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    public async Task ExecuteAsync_SeekableUploadOnADeadReusedConnection_SendsTheWholeBodyOnAFreshConnection()
    {
        byte[] content = Encoding.ASCII.GetBytes("hello upload");
        MemoryStream body = new(content);
        ScriptedConnection dead = new([], 65536);
        ScriptedConnection fresh = new(Encoding.ASCII.GetBytes(KeepAlive200), 65536);
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(fresh, null, connectionNumber: 1));

        TransferResult result = await Handler(connector).ExecuteAsync(Context(new StreamBody(body, content.Length, "multipart/form-data; boundary=b")));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual(1, result.Report.ConnectionCount);
        Assert.EndsWith("\r\n\r\nhello upload", Encoding.ASCII.GetString(fresh.Written));
        Assert.Contains("Content-Length: 12", Encoding.ASCII.GetString(fresh.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_SeekableUploadFollowingA307_ReachesTheRedirectTargetWholeOnAFreshConnection()
    {
        // The engine follows -L: it runs the handler again with the same body, rewound, and the
        // server has closed the connection that carried the 307.
        byte[] content = Encoding.ASCII.GetBytes("redirected body");
        MemoryStream body = new(content);
        ScriptedConnection redirecting = new(Encoding.ASCII.GetBytes("HTTP/1.1 307 Temporary Redirect\r\nLocation: /redir-target\r\nContent-Length: 0\r\n\r\n"), 65536);
        ScriptedConnection dead = new([], 65536);
        ScriptedConnection fresh = new(Encoding.ASCII.GetBytes(KeepAlive200), 65536);
        QueueConnector connector = new(
            ConnectResult.Connected(redirecting, null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(dead, null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(fresh, null, connectionNumber: 1));
        HttpProtocolHandler handler = Handler(connector);

        TransferResult first = await handler.ExecuteAsync(Context(new StreamBody(body, content.Length, "application/octet-stream"), followRedirects: true));
        body.Position = 0;
        TransferResult second = await handler.ExecuteAsync(Context(new StreamBody(body, content.Length, "application/octet-stream"), followRedirects: true));

        Assert.AreEqual(307, first.Report!.ResponseCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(200, second.Report!.ResponseCode);
        Assert.EndsWith("\r\n\r\nredirected body", Encoding.ASCII.GetString(fresh.Written));
    }
    [TestMethod]
    public async Task ExecuteAsync_NonSeekableUploadOnADeadReusedConnection_IsNotSentAgain()
    {
        byte[] content = Encoding.ASCII.GetBytes("from stdin");
        ScriptedConnection dead = new([], 65536);
        ScriptedConnection fresh = new(Encoding.ASCII.GetBytes(KeepAlive200), 65536);
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(fresh, null, connectionNumber: 1));

        TransferResult result = await Handler(connector).ExecuteAsync(
            Context(new StreamBody(new NonSeekableStream(content), content.Length, "application/octet-stream")));

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(1, connector.Targets.Count);
        Assert.AreEqual(0, fresh.Written.Length);
    }

    private static HttpProtocolHandler Handler(QueueConnector connector) => new(connector, new SilentAuthenticator());

    private static TransferContext Context(HttpRequestBody body, bool followRedirects = false) =>
        new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Body = body, FollowRedirects = followRedirects },
        };

    private sealed class NonSeekableStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
