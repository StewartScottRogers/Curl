using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3ClientRequestStream" /> over an in-memory connection: HEADERS from any
/// field list (extended CONNECT's <c>:protocol</c> included), DATA and the end of the request
/// going out, and the response's HEADERS, DATA and end, a reset and the session's connection
/// error coming in.
/// </summary>
[TestClass]
public sealed class Http3ClientRequestStreamTests
{
    private static readonly Http3Setting[] LocalSettings = [];

    [TestMethod]
    public async Task SendHeadersAsync_ExtendedConnect_SendsEveryPseudoHeader()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);
        HeaderField[] fields = [new(":method", "CONNECT"), new(":protocol", "websocket"), new(":scheme", "https"), new(":authority", "example.com"), new(":path", "/chat")];

        int length = await request.SendHeadersAsync(fields, endStream: false, CancellationToken.None);

        InMemoryMultiplexedStream stream = connection.BidirectionalStreams[0];
        Assert.AreSame(stream, request.Stream);
        Http3HeadersFrame sent = (Http3HeadersFrame)(await Http3.ReadAllFramesAsync(new MemoryStream(stream.Written.ToArray())))[0];
        Assert.AreEqual(length, sent.EncodedFieldSection.Length);
        CollectionAssert.AreEqual(fields, request.DecodeFieldSection(sent).ToArray());
        Assert.IsFalse(stream.IsWriteEnded);
    }

    [TestMethod]
    public async Task SendDataAsyncAndEndRequestAsync_WriteDataThenTheEnd()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);

        await request.SendDataAsync(new byte[] { 0x61, 0x62 }, endStream: false, CancellationToken.None);
        await request.EndRequestAsync(CancellationToken.None);

        InMemoryMultiplexedStream stream = connection.BidirectionalStreams[0];
        CollectionAssert.AreEqual(Qpack.FromHex("00 02 61 62"), stream.Written.ToArray());
        Assert.IsTrue(stream.IsWriteEnded);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_Response_ReadsHeadersDataAndEnd()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);
        byte[] section = session.Encoder.EncodeFieldSection(0, [new(":status", "200")]);
        InMemoryMultiplexedStream stream = connection.BidirectionalStreams[0];
        stream.Receive(new Http3HeadersFrame(section).ToBytes()).Receive(Qpack.FromHex("00 02 6f 6b"));
        stream.EndReceiving();
        byte[] buffer = new byte[16];

        Http3FrameOrData headers = await request.ReadFrameOrDataAsync(buffer, CancellationToken.None);
        Http3FrameOrData data = await request.ReadFrameOrDataAsync(buffer, CancellationToken.None);
        Http3FrameOrData end = await request.ReadFrameOrDataAsync(buffer, CancellationToken.None);

        Assert.AreEqual(new HeaderField(":status", "200"), request.DecodeFieldSection((Http3HeadersFrame)headers.Frame!)[0]);
        Assert.AreEqual(2, data.DataLength);
        Assert.AreEqual(0x6f, buffer[0]);
        Assert.IsTrue(end.IsEndOfStream);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_StreamReset_ThrowsTheReset()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);
        connection.BidirectionalStreams[0].FailReceiving(new MultiplexedStreamResetException((long)Http3ErrorCode.RequestRejected, "rejected"));

        MultiplexedStreamResetException reset = await Assert.ThrowsExactlyAsync<MultiplexedStreamResetException>(async () => await request.ReadFrameOrDataAsync(new byte[16], CancellationToken.None));

        Assert.AreEqual((long)Http3ErrorCode.RequestRejected, reset.ApplicationErrorCode);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_ConnectionErrorBeforeOrDuringTheRead_ThrowsIt()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);
        Task<Http3Exception> during = Assert.ThrowsExactlyAsync<Http3Exception>(async () => await request.ReadFrameOrDataAsync(new byte[16], CancellationToken.None));
        connection.AddPeerStream(0x01).EndReceiving();
        await session.PeerStreamsReading;
        connection.BidirectionalStreams[0].FailReceiving(new IOException("closed"));

        Exception duringError = await during;
        Exception before = await Assert.ThrowsExactlyAsync<Http3Exception>(async () => await request.ReadFrameOrDataAsync(new byte[16], CancellationToken.None));

        Assert.AreSame(session.ConnectionError, before);
        Assert.AreSame(session.ConnectionError, duringError);
    }

    [TestMethod]
    public async Task ReadFrameOrDataAsync_StreamFailsWithoutConnectionError_ThrowsTheStreamFailure()
    {
        InMemoryMultiplexedConnection connection = new();
        await using Http3ClientSession session = new(connection, LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);
        connection.BidirectionalStreams[0].FailReceiving(new IOException("closed"));

        await Assert.ThrowsExactlyAsync<IOException>(async () => await request.ReadFrameOrDataAsync(new byte[16], CancellationToken.None));
    }

    [TestMethod]
    public async Task DecodeFieldSection_Null_Throws()
    {
        await using Http3ClientSession session = new(new InMemoryMultiplexedConnection(), LocalSettings);
        Http3ClientRequestStream request = await session.OpenRequestStreamAsync(null, CancellationToken.None);

        Assert.ThrowsExactly<ArgumentNullException>(() => request.DecodeFieldSection(null!));
    }
}
