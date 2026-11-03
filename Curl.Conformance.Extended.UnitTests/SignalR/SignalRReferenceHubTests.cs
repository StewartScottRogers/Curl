using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;

namespace Curl.Conformance.Extended.UnitTests.SignalR;

/// <summary>The reference hub's own answers to what curl never sends.</summary>
[TestClass]
public sealed class SignalRReferenceHubTests
{
    private const string Upgrade = "GET /hub?id=t HTTP/1.1\r\nHost: x\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n";

    private static async Task<byte[]> ServeAsync(SignalRReferenceHub hub, byte[] input)
    {
        using MemoryStream reads = new(input);
        using DuplexStream stream = new(reads);
        await hub.ServeAsync(new ReferenceConnection(stream, new WireExchange(0, ReferenceTransport.Tcp)), CancellationToken.None);
        return stream.Written.ToArray();
    }

    private static byte[] MaskedClose() => [0x88, 0x82, 0, 0, 0, 0, 0x03, 0xE8];

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedBeforeARequest_AnswersNothing()
    {
        SignalRReferenceHub hub = new(["{}"]);

        byte[] written = await ServeAsync(hub, Encoding.ASCII.GetBytes("POST /hub/nego"));

        Assert.IsEmpty(written);
        Assert.IsEmpty(hub.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_ScriptReceiveSeesTheConnectionClose_EndsTheScript()
    {
        SignalRReferenceHub hub = new(["{}"], SignalRHubStep.Receive, SignalRHubStep.Records("{}"));

        byte[] written = await ServeAsync(hub, Encoding.ASCII.GetBytes(Upgrade));

        StringAssert.Contains(Encoding.ASCII.GetString(written), "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n");
        StringAssert.EndsWith(Encoding.ASCII.GetString(written), "\r\n\r\n");
    }

    [TestMethod]
    public async Task ServeAsync_CloseAfterTheHubsOwnClose_IsNotAnsweredAgain()
    {
        SignalRReferenceHub hub = new(["{}"], SignalRHubStep.Send(0x88, [0x03, 0xE8]));

        byte[] written = await ServeAsync(hub, [.. Encoding.ASCII.GetBytes(Upgrade), .. MaskedClose(), 0x8A, 0x80, 0, 0, 0, 0]);

        CollectionAssert.AreEqual(new byte[] { 0x88, 0x02, 0x03, 0xE8 }, written[^4..]);
        Assert.HasCount(2, hub.ClientFrames);
    }

    [TestMethod]
    public async Task ServeAsync_ClientCloseWithNoScriptedClose_IsAnsweredOnce()
    {
        SignalRReferenceHub hub = new(["{}"]);

        byte[] written = await ServeAsync(hub, [.. Encoding.ASCII.GetBytes(Upgrade), .. MaskedClose(), .. MaskedClose()]);

        CollectionAssert.AreEqual(new byte[] { 0x88, 0x02, 0x03, 0xE8 }, written[^4..]);
        Assert.AreEqual(1, written.Count(octet => octet == 0x88));
    }

    [TestMethod]
    public void TryRead_UnmaskedFrameWithA16BitLength_ReadsItsPayload()
    {
        byte[] payload = [.. Enumerable.Repeat((byte)'a', 300)];
        byte[] frame = SignalRReferenceFrame.Write(0x01, payload);

        int taken = SignalRReferenceFrame.TryRead(frame, out SignalRReferenceFrame? read);

        Assert.AreEqual(frame.Length, taken);
        Assert.IsNotNull(read);
        Assert.IsFalse(read.IsFinal);
        Assert.IsFalse(read.IsMasked);
        CollectionAssert.AreEqual(payload, read.Payload);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81 })]
    [DataRow(new byte[] { 0x81, 0x85, 1, 2, 3, 4, 5 })]
    public void TryRead_PartOfAFrame_TakesNothing(byte[] pending)
    {
        int taken = SignalRReferenceFrame.TryRead(pending, out SignalRReferenceFrame? read);

        Assert.AreEqual(0, taken);
        Assert.IsNull(read);
    }

    [TestMethod]
    public void TryRead_RequestWithABodyNotYetWhole_TakesNothing()
    {
        int taken = SignalRReferenceRequest.TryRead("POST / HTTP/1.1\r\nContent-Length: 4\r\n\r\nab"u8, out SignalRReferenceRequest? request);

        Assert.AreEqual(0, taken);
        Assert.IsNull(request);
    }

    [TestMethod]
    public void TryRead_RequestWithABody_ReadsItAndItsHeaders()
    {
        int taken = SignalRReferenceRequest.TryRead("POST /n HTTP/1.1\r\ncontent-length: 2\r\n\r\nokNEXT"u8, out SignalRReferenceRequest? request);

        Assert.AreEqual(41, taken);
        Assert.AreEqual("ok", request!.Body);
        Assert.AreEqual("2", request.Header("Content-Length"));
        Assert.IsNull(request.Header("Upgrade"));
    }

    private sealed class DuplexStream(Stream reads) : Stream
    {
        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => reads.Read(buffer, offset, Math.Min(count, 3));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);
    }
}
