using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Grpc.Client;

/// <summary>
/// Checks the gRPC handler against grpc.io's PROTOCOL-HTTP2.md over a scripted connection: the request it sends
/// ("Requests": Request-Headers and one Length-Prefixed-Message) and how it reads each reply ("Responses").
/// </summary>
[TestClass]
public sealed class GrpcProtocolHandlerTests
{
    private const string GreeterUrl = "grpc://example.com:50051/helloworld.Greeter/SayHello";

    private static readonly byte[] HelloWorld = [0x0A, 0x05, .. "World"u8];

    private static readonly byte[] ReplyHi = [0x0A, 0x02, .. "Hi"u8];

    private static readonly string HiJson = "{\n  \"message\": \"Hi\"\n}\n";

    [TestMethod]
    public async Task ExecuteAsync_Requests_SendsTheRequestHeadersAndOneLengthPrefixedMessage()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{\"name\":\"World\"}", TimeSpan.FromSeconds(2));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        (List<HeaderField> headers, byte[] data) = await SentRequestAsync(call.Connection.Sent);
        CollectionAssert.AreEqual(
            new[] { ":method: POST", ":scheme: http", ":path: /helloworld.Greeter/SayHello", ":authority: example.com:50051", "te: trailers", "grpc-timeout: 2000000u", "content-type: application/grpc" },
            headers.Select(static field => $"{field.Name}: {field.Value}").ToArray(),
            string.Join(" | ", headers.Select(static field => $"{field.Name}: {field.Value}")));
        CollectionAssert.AreEqual((byte[])[0, 0, 0, 0, 7, .. HelloWorld], data);
        Assert.AreEqual(("example.com", 50051, false), (call.Connector.Target!.Host, call.Connector.Target.Port, call.Connector.Target.UseTls));
        CollectionAssert.AreEqual(new[] { "h2" }, call.Connector.Target!.ApplicationProtocols!.ToArray());
        Assert.IsTrue(call.Connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseMessageAndOkTrailers_WritesTheMessageAsJson()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{\"name\":\"World\"}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(HiJson, call.Output);
        Assert.AreEqual(HiJson.Length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutPortOrData_CallsPort80WithTheEmptyMessage()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "0")));

        TransferResult result = await call.RunAsync("grpc://example.com/helloworld.Greeter/SayHello", proto.GreeterPath, null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(80, call.Connector.Target!.Port);
        (List<HeaderField> headers, byte[] data) = await SentRequestAsync(call.Connection.Sent);
        Assert.AreEqual("example.com", headers.Single(static field => field.Name == ":authority").Value);
        Assert.IsFalse(headers.Any(static field => field.Name == "grpc-timeout"));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0 }, data);
    }

    [TestMethod]
    public async Task ExecuteAsync_GrpcsNegotiatingH2_CallsOverTlsToPort443AsHttps()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)), "h2");

        TransferResult result = await call.RunAsync("grpcs://example.com/helloworld.Greeter/SayHello", proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(call.Connector.Target!.UseTls);
        Assert.AreEqual(443, call.Connector.Target.Port);
        (List<HeaderField> headers, _) = await SentRequestAsync(call.Connection.Sent);
        Assert.AreEqual("https", headers.Single(static field => field.Name == ":scheme").Value);
        Assert.AreEqual(HiJson, call.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_GrpcsWithoutH2_FailsWithExit16()
    {
        using var proto = new ProtoFixture();
        var call = new Call([], "http/1.1");

        TransferResult result = await call.RunAsync("grpcs://example.com/helloworld.Greeter/SayHello", proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("The server at example.com did not negotiate h2 with ALPN, so it cannot take a gRPC call", result.ErrorMessage);
        Assert.IsEmpty(call.Connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_EndsWithTheConnectorsExitCode()
    {
        using var proto = new ProtoFixture();
        var connector = new ScriptedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));
        var context = new GrpcProtoContext(new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream() }, proto.GreeterPath);

        TransferResult result = await new GrpcProtocolHandler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonZeroGrpcStatus_WritesTheMessagesAndFailsWithExit22AndGrpcMessage()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(5, "no%20such user")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("grpc-status 5 (NOT_FOUND): no such user", result.ErrorMessage);
        Assert.AreEqual(HiJson, call.Output);
        Assert.AreEqual(HiJson.Length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_TrailersOnlyError_FailsWithExit22()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "12", "grpc-message", "unknown method")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("grpc-status 12 (UNIMPLEMENTED): unknown method", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpStatus404WithoutGrpcStatus_FailsAsUnimplemented()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "404")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("grpc-status 12 (UNIMPLEMENTED): HTTP status 404", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedMessage_FailsWithExit61()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Data(Wire.LengthPrefixedMessage.Frame(ReplyHi, isCompressed: true)), GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageNotOfTheOutputType_FailsWithExit8()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message([0xFF]), GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.StartsWith("The server's message is not a helloworld.HelloReply", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_StreamEndsWithoutTrailers_FailsWithExit8()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), Http2FrameFactory.CreateData(1, Wire.LengthPrefixedMessage.Frame(ReplyHi, false), isEndStream: true)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("A gRPC response ended without trailers.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionClosedBeforeTrailers_FailsWithExit16()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("The server closed the HTTP/2 connection before the gRPC call ended", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2Failures_FailWithExit16()
    {
        byte[][] replies =
        [
            GrpcServerReply.Of(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.InternalError)),
            GrpcServerReply.Of(Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.InternalError, ReadOnlyMemory<byte>.Empty)),
            GrpcServerReply.Of(Http2FrameFactory.CreateHeaders(1, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, isEndStream: false, isEndHeaders: true)),
            [.. GrpcServerReply.Of(), 0, 0, 7, (byte)Http2FrameType.Ping, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7],
            [.. GrpcServerReply.Of(), 0, 0, 9],
        ];
        using var proto = new ProtoFixture();

        foreach (byte[] reply in replies)
        {
            TransferResult result = await new Call(reply).RunAsync(GreeterUrl, proto.GreeterPath, "{}");

            Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_MessagePastTheSendWindow_SendsTheRestAfterWindowUpdates()
    {
        using var proto = new ProtoFixture();
        string name = new('x', 70000);
        var call = new Call(GrpcServerReply.Of(
            Http2FrameFactory.CreateWindowUpdate(0, 100000),
            Http2FrameFactory.CreateWindowUpdate(1, 100000),
            GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders),
            GrpcServerReply.Message(ReplyHi),
            GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, $"{{\"name\":\"{name}\"}}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        (_, byte[] data) = await SentRequestAsync(call.Connection.Sent);
        Assert.HasCount(5 + 4 + 70000, data);
        Assert.AreEqual(HiJson, call.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyBeforeTheMessageIsSent_StopsSendingAndReadsIt()
    {
        using var proto = new ProtoFixture();
        string name = new('x', 70000);
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "grpc-status", "8", "grpc-message", "too big")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, $"{{\"name\":\"{name}\"}}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("grpc-status 8 (RESOURCE_EXHAUSTED): too big", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionClosedWhileSending_FailsWithExit16()
    {
        using var proto = new ProtoFixture();
        string name = new('x', 70000);
        var call = new Call(GrpcServerReply.Of());

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, $"{{\"name\":\"{name}\"}}");

        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutGrpcProto_FailsWithExit2BeforeConnecting()
    {
        var call = new Call([]);

        TransferResult result = await new GrpcProtocolHandler(call.Connector).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("A gRPC call needs --grpc-proto <file>: calling without one (server reflection) is not supported yet", result.ErrorMessage);
        Assert.IsNull(call.Connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathNotServiceAndMethod_FailsWithExit3()
    {
        using var proto = new ProtoFixture();

        TransferResult result = await new Call([]).RunAsync("grpc://example.com/helloworld.Greeter", proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataNotJsonOfTheInputType_FailsWithExit2()
    {
        using var proto = new ProtoFixture();

        TransferResult result = await new Call([]).RunAsync(GreeterUrl, proto.GreeterPath, "{\"nope\":1}");

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.StartsWith("-d: the data is not a helloworld.HelloRequest message as JSON", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "x"))).ExecuteAsync(null!));

    [TestMethod]
    [DataRow("grpc://h/a/b", true)]
    [DataRow("GRPCS://h/a/b", true)]
    [DataRow("http://h/a/b", false)]
    public void Serves_GivesTrueForGrpcAndGrpcsOnly(string url, bool expected) =>
        Assert.AreEqual(expected, GrpcProtocolHandler.Serves(CurlUrl.Parse(url)));

    [TestMethod]
    public void SupportedSchemes_AreGrpcAndGrpcs() =>
        CollectionAssert.AreEqual(new[] { "grpc", "grpcs" }, new GrpcProtocolHandler(null!).SupportedSchemes.ToArray());

    /// <summary>Reads the request's header fields and its DATA bytes from what the client sent after the connection preface.</summary>
    private static async Task<(List<HeaderField> Headers, byte[] Data)> SentRequestAsync(byte[] sent)
    {
        using var stream = new MemoryStream(sent, Http2Connection.ClientPreface.Length, sent.Length - Http2Connection.ClientPreface.Length);
        List<HeaderField> headers = [];
        var data = new MemoryStream();
        while (await Http2FrameCodec.ReadAsync(stream, 1 << 20, CancellationToken.None) is { } frame)
        {
            if (frame.StreamId == 1 && frame.Type == Http2FrameType.Headers)
            {
                headers.AddRange(new HpackDecoder().Decode(frame.Payload.Span));
            }
            else if (frame.StreamId == 1 && frame.Type == Http2FrameType.Data)
            {
                data.Write(frame.Payload.Span);
            }
        }

        return (headers, data.ToArray());
    }

    /// <summary>One call through the handler over a scripted connection.</summary>
    private sealed class Call(byte[] reply, string? applicationProtocol = null)
    {
        private readonly MemoryStream output = new();

        public ScriptedConnection Connection { get; } = new(reply);

        public ScriptedConnector Connector { get; } = new(ConnectResult.Connected(new ScriptedConnection(reply), null, applicationProtocol: applicationProtocol));

        public string Output => Encoding.UTF8.GetString(output.ToArray());

        public async Task<TransferResult> RunAsync(string url, string protoPath, string? data, TimeSpan? maxTime = null)
        {
            var connector = new RecordingConnector(this, applicationProtocol);
            var context = new TransferContext
            {
                Url = CurlUrl.Parse(url),
                Output = output,
                PostData = data is null ? null : Encoding.UTF8.GetBytes(data),
                MaxTime = maxTime,
            };
            return await new GrpcProtocolHandler(connector).ExecuteAsync(new GrpcProtoContext(context, protoPath));
        }

        private sealed class RecordingConnector(Call call, string? applicationProtocol) : IConnector
        {
            public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
            {
                _ = call.Connector.ConnectAsync(target, cancellationToken);
                return ValueTask.FromResult(ConnectResult.Connected(call.Connection, null, applicationProtocol: applicationProtocol));
            }
        }
    }
}
