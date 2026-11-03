using Curl.Conformance.Extended.Grpc;
using Curl.Conformance.Extended.Harness;
using Curl.Http2;

namespace Curl.Conformance.Extended.UnitTests.Grpc;

/// <summary>The reference gRPC server's own edges, driven by fixed client bytes rather than a curl run.</summary>
[TestClass]
public sealed class GrpcReferenceServerTests
{
    private const string MethodPath = "/demo.Greeter/SayHello";

    private static readonly byte[] Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();

    [TestMethod]
    public async Task ServeAsync_ClientClosesInsidePreface_SendsNothing()
    {
        (GrpcReferenceServer server, List<Http2Frame> sent) = await ServeAsync(Preface[..10]);

        Assert.IsEmpty(sent);
        Assert.IsEmpty(server.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_OtherBytesThanPreface_SendsNothing()
    {
        (_, List<Http2Frame> sent) = await ServeAsync(new byte[Preface.Length]);

        Assert.IsEmpty(sent);
    }

    [TestMethod]
    public async Task ServeAsync_SettingsPingAndWindowUpdate_AcknowledgesSettingsAndPingOnly()
    {
        byte[] client = Join(
            Preface,
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreatePing(7, isAcknowledgement: false)),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreatePing(8, isAcknowledgement: true)),
            Http2FrameCodec.Serialize(new Http2Frame(Http2FrameType.WindowUpdate, 0, 0, new byte[] { 0, 0, 1, 0 })));

        (_, List<Http2Frame> sent) = await ServeAsync(client);

        Assert.HasCount(3, sent);
        Assert.AreEqual(Http2FrameType.Settings, sent[0].Type);
        Assert.IsTrue(sent[1].HasFlag(Http2FrameFlags.Acknowledgement));
        Assert.AreEqual(7UL, Http2FramePayloadParser.ParsePing(sent[2]));
        Assert.IsTrue(sent[2].HasFlag(Http2FrameFlags.Acknowledgement));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesInsideFrame_Ends()
    {
        byte[] frame = Http2FrameCodec.Serialize(Http2FrameFactory.CreatePing(7, isAcknowledgement: false));

        (_, List<Http2Frame> sent) = await ServeAsync(Join(Preface, frame[..4]));
        (_, List<Http2Frame> sentAfterHeader) = await ServeAsync(Join(Preface, frame[..12]));

        Assert.HasCount(1, sent);
        Assert.HasCount(1, sentAfterHeader);
    }

    [TestMethod]
    public async Task ServeAsync_HeadersInContinuationThenSplitMessage_RecordsTheCallAndSendsTheScriptedReply()
    {
        byte[] block = new HpackEncoder().Encode([new(":method", "POST"), new(":path", MethodPath), new("te", "trailers")]);
        byte[] message = GrpcLengthPrefixedMessage.Frame([0x0A, 0x01, (byte)'x']);
        byte[] client = Join(
            Preface,
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, block.AsMemory(0, 3), isEndStream: false, isEndHeaders: false)),
            Http2FrameCodec.Serialize(new Http2Frame(Http2FrameType.Continuation, Http2FrameFlags.EndHeaders, 1, block.AsMemory(3))),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, message.AsMemory(0, 2), isEndStream: false)),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, message.AsMemory(2), isEndStream: true)));
        GrpcReferenceReply reply = GrpcReferenceReply.Ok([1], [2]);

        (GrpcReferenceServer server, List<Http2Frame> sent) = await ServeAsync(client, new GrpcScriptedService(MethodPath, reply));

        GrpcReferenceCall call = server.Calls.Single();
        Assert.AreEqual(1, call.StreamId);
        Assert.AreEqual("trailers", call.Header("te"));
        Assert.IsNull(call.Header("grpc-timeout"));
        Assert.IsTrue(call.IsRequestEnded);
        Assert.HasCount(2, call.DataFrames);
        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x01, (byte)'x' }, call.Messages.Single().Payload);
        Assert.IsFalse(call.Messages.Single().IsCompressed);
        HpackDecoder decoder = new();
        CollectionAssert.AreEqual(new[] { Http2FrameType.Settings, Http2FrameType.Headers, Http2FrameType.Data, Http2FrameType.Data, Http2FrameType.Headers }, sent.Select(static frame => frame.Type).ToArray());
        CollectionAssert.AreEqual(GrpcReferenceReply.OkHeaders.ToArray(), decoder.Decode(sent[1].Payload.Span).ToArray());
        CollectionAssert.AreEqual(GrpcLengthPrefixedMessage.Frame([2]), sent[3].Payload.ToArray());
        Assert.AreEqual("0", decoder.Decode(sent[4].Payload.Span).Single(static field => field.Name == "grpc-status").Value);
        Assert.IsTrue(sent[4].HasFlag(Http2FrameFlags.EndStream));
    }

    [TestMethod]
    public async Task ServeAsync_NoServiceAnswersEndedRequest_SendsTrailersOnlyUnimplemented()
    {
        byte[] block = new HpackEncoder().Encode([new(":method", "POST")]);
        byte[] client = Join(
            Preface,
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(5, new byte[] { 1 }, isEndStream: true)),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, block, isEndStream: true, isEndHeaders: true)),
            Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, ReadOnlyMemory<byte>.Empty, isEndStream: true)));

        (GrpcReferenceServer server, List<Http2Frame> sent) = await ServeAsync(client, new GrpcScriptedService(MethodPath, GrpcReferenceReply.Ok()));

        Assert.AreEqual(string.Empty, server.Calls.Single().Path);
        Assert.HasCount(2, sent);
        Assert.IsTrue(sent[1].HasFlag(Http2FrameFlags.EndStream));
        IReadOnlyList<HeaderField> fields = new HpackDecoder().Decode(sent[1].Payload.Span);
        Assert.AreEqual("12", fields.Single(static field => field.Name == "grpc-status").Value);
        Assert.AreEqual("No service serves ", fields.Single(static field => field.Name == "grpc-message").Value);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyWithoutTrailers_EndsTheStreamWithAnEmptyDataFrame()
    {
        byte[] block = new HpackEncoder().Encode([new(":path", MethodPath)]);
        byte[] client = Join(Preface, Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, block, isEndStream: true, isEndHeaders: true)));
        GrpcReferenceReply reply = new(GrpcReferenceReply.OkHeaders, [], []);

        (_, List<Http2Frame> sent) = await ServeAsync(client, new GrpcScriptedService(MethodPath, reply));

        Assert.HasCount(3, sent);
        Assert.AreEqual(Http2FrameType.Data, sent[2].Type);
        Assert.IsTrue(sent[2].HasFlag(Http2FrameFlags.EndStream));
        Assert.IsTrue(sent[2].Payload.IsEmpty);
    }

    [TestMethod]
    public async Task ServeAsync_ResetReply_SendsRstStreamWithItsErrorCodeOnly()
    {
        (_, List<Http2Frame> sent) = await ServeAsync(EndedRequest(), new GrpcScriptedService(MethodPath, GrpcReferenceReply.Reset(Http2ErrorCode.RefusedStream)));

        Assert.HasCount(2, sent);
        Assert.AreEqual((Http2FrameType.RstStream, 1), (sent[1].Type, sent[1].StreamId));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 7 }, sent[1].Payload.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_GoAwayReply_SendsGoAwayWithItsLastStreamIdOnly()
    {
        (_, List<Http2Frame> sent) = await ServeAsync(EndedRequest(), new GrpcScriptedService(MethodPath, GrpcReferenceReply.GoAway(0)));

        Assert.HasCount(2, sent);
        Assert.AreEqual((Http2FrameType.GoAway, 0), (sent[1].Type, sent[1].StreamId));
        CollectionAssert.AreEqual(new byte[8], sent[1].Payload.ToArray());
    }

    [TestMethod]
    [DataRow(0x0102030405060708UL, true)]
    [DataRow(9UL, false)]
    public async Task ServeAsync_PingBeforeReply_SendsThePingFirstAndRecordsWhetherItWasEchoed(ulong acknowledged, bool isEchoed)
    {
        GrpcReferencePing ping = new(0x0102030405060708);
        GrpcReferenceReply reply = GrpcReferenceReply.Ok() with { PingBeforeReply = ping };
        byte[] client = Join(EndedRequest(), Http2FrameCodec.Serialize(Http2FrameFactory.CreatePing(acknowledged, isAcknowledgement: true)));

        (_, List<Http2Frame> sent) = await ServeAsync(client, new GrpcScriptedService(MethodPath, reply));

        Assert.AreEqual(Http2FrameType.Ping, sent[1].Type);
        Assert.IsFalse(sent[1].HasFlag(Http2FrameFlags.Acknowledgement));
        Assert.AreEqual(ping.Payload, Http2FramePayloadParser.ParsePing(sent[1]));
        Assert.AreEqual(Http2FrameType.Headers, sent[2].Type);
        Assert.AreEqual(isEchoed, ping.IsEchoed);
    }

    [TestMethod]
    public void TrailersOnly_WithoutMessage_CarriesGrpcStatusOnly()
    {
        GrpcReferenceReply reply = GrpcReferenceReply.TrailersOnly(5);

        Assert.IsTrue(reply.IsTrailersOnly);
        Assert.AreEqual(new HeaderField("grpc-status", "5"), reply.Trailers.Single());
    }

    [TestMethod]
    public void Parse_CompressedMessageThenOneCutShort_ReadsTheFirstAndFailsOnTheSecond()
    {
        byte[] compressed = GrpcLengthPrefixedMessage.Frame([9], isCompressed: true);

        GrpcReferenceMessage message = GrpcLengthPrefixedMessage.Parse(compressed).Single();

        Assert.IsTrue(message.IsCompressed);
        Assert.ThrowsExactly<InvalidDataException>(() => GrpcLengthPrefixedMessage.Parse(Join(compressed, compressed[..5])));
        Assert.ThrowsExactly<InvalidDataException>(() => GrpcLengthPrefixedMessage.Parse(compressed[..3]));
    }

    private static async Task<(GrpcReferenceServer Server, List<Http2Frame> Sent)> ServeAsync(byte[] client, params IReadOnlyList<IGrpcReferenceService> services)
    {
        GrpcReferenceServer server = new(services);
        using ClientBytesStream stream = new(client);

        await server.ServeAsync(new ReferenceConnection(stream, new WireExchange(50051, ReferenceTransport.Tcp)), CancellationToken.None);

        List<Http2Frame> sent = [];
        using MemoryStream written = new(stream.Written.ToArray());
        while (await Http2FrameCodec.ReadAsync(written, Http2FrameCodec.DefaultMaximumFrameSize, CancellationToken.None) is { } frame)
        {
            sent.Add(frame);
        }

        return (server, sent);
    }

    private static byte[] EndedRequest() =>
        Join(Preface, Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode([new(":path", MethodPath)]), isEndStream: true, isEndHeaders: true)));

    private static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(static part => part)];

    /// <summary>A connection that replays the client's bytes and keeps what the server writes apart from them.</summary>
    private sealed class ClientBytesStream(byte[] client) : MemoryStream(client, writable: false)
    {
        public MemoryStream Written { get; } = new();

        public override bool CanWrite => true;

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => Written.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
