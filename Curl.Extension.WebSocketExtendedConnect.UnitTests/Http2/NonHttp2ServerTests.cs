using System.Text;

using Curl.Http2;
using Curl.WebSocket.Fakes;

using static Curl.Extension.WebSocketExtendedConnect.Http2.ScriptedServerFrames;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

[TestClass]
public sealed class NonHttp2ServerTests
{
    private const string SettingsExpected =
        "Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.";

    [TestMethod]
    public async Task OpenAsync_Http1ReplyInsteadOfSettings_FailsWithTheSettingsExpectedText()
    {
        using var server = new RawServerStream(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n"));

        var failure = await Assert.ThrowsExactlyAsync<Http2WebSocketException>(() => OpenAsync(server));

        Assert.AreEqual(SettingsExpected, failure.Message);
        Assert.IsInstanceOfType<Http2ProtocolException>(failure.InnerException);
    }

    [TestMethod]
    public async Task OpenAsync_FrameSizeErrorAfterSettings_KeepsTheProtocolException()
    {
        byte[] settings = Http2FrameCodec.Serialize(ServerSettings(EnableConnectProtocol)).ToArray();
        byte[] oversized = [0x3F, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
        using var server = new RawServerStream([.. settings, .. oversized]);

        var error = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => OpenAsync(server));

        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error.ErrorCode);
    }

    private static Task<Http2WebSocket> OpenAsync(Stream server) =>
        Http2WebSocketClient.OpenAsync(server, "http", "/ws", "example.com", [], new FixedRandomSource(), CancellationToken.None);

    private sealed class RawServerStream(byte[] serverBytes) : Stream
    {
        private readonly MemoryStream reads = new(serverBytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => reads.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            reads.ReadAsync(buffer, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
