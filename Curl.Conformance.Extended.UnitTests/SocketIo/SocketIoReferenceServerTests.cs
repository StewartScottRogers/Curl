using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SocketIo;

namespace Curl.Conformance.Extended.UnitTests.SocketIo;

/// <summary>The reference server's own answers to what curl never sends.</summary>
[TestClass]
public sealed class SocketIoReferenceServerTests
{
    private const string Upgrade = "GET /socket.io/?EIO=4&transport=websocket HTTP/1.1\r\nHost: x\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n";

    private static async Task<string> ServeAsync(SocketIoReferenceServer server, byte[] input)
    {
        using MemoryStream reads = new(input);
        using DuplexStream stream = new(reads);
        await server.ServeAsync(new ReferenceConnection(stream, new WireExchange(0, ReferenceTransport.Tcp)), CancellationToken.None);
        return Encoding.Latin1.GetString(stream.Written.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedBeforeARequest_AnswersNothing()
    {
        SocketIoReferenceServer server = new();

        string written = await ServeAsync(server, Encoding.ASCII.GetBytes("GET /socket"));

        Assert.IsEmpty(written);
        Assert.IsEmpty(server.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_RequestThatIsNoUpgrade_IsAnswered400()
    {
        SocketIoReferenceServer server = new();

        string written = await ServeAsync(server, Encoding.ASCII.GetBytes("GET /socket.io/?EIO=4&transport=polling HTTP/1.1\r\nHost: x\r\n\r\n"));

        StringAssert.StartsWith(written, "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_ScriptReceiveSeesTheConnectionClose_EndsTheScript()
    {
        SocketIoReferenceServer server = new(SocketIoStep.Receive, SocketIoStep.Packet("41"));

        string written = await ServeAsync(server, Encoding.ASCII.GetBytes(Upgrade));

        StringAssert.EndsWith(written, "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CloseAfterTheServersOwnClose_IsNotAnsweredAgain()
    {
        SocketIoReferenceServer server = new(SocketIoStep.Send(0x88, [0x03, 0xE8]));

        string written = await ServeAsync(server, [.. Encoding.ASCII.GetBytes(Upgrade), 0x88, 0x82, 0, 0, 0, 0, 0x03, 0xE8]);

        StringAssert.EndsWith(written, "\r\n\r\n\u0088\u0002\u0003è");
        Assert.HasCount(1, server.ClientFrames);
    }

    [TestMethod]
    public async Task ServeAsync_ClientCloseWithNoScriptedClose_IsAnsweredWithACloseAndTheTextIsAPacket()
    {
        SocketIoReferenceServer server = new();

        string written = await ServeAsync(server, [.. Encoding.ASCII.GetBytes(Upgrade), 0x81, 0x82, 0, 0, 0, 0, (byte)'4', (byte)'0', 0x88, 0x80, 0, 0, 0, 0]);

        StringAssert.EndsWith(written, "\r\n\r\n\u0088\u0000");
        CollectionAssert.AreEqual(new[] { "40" }, server.ClientPackets.ToArray());
    }

    /// <summary>A stream that reads from <paramref name="reads" /> and keeps what is written.</summary>
    private sealed class DuplexStream(Stream reads) : Stream
    {
        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => reads.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Written.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
