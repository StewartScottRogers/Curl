using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

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
            new[] { ":method: POST", ":scheme: http", ":path: /helloworld.Greeter/SayHello", ":authority: example.com:50051", "grpc-timeout: 2000000u", "te: trailers", "content-type: application/grpc", "user-agent: curl/8.22.0" },
            headers.Select(static field => $"{field.Name}: {field.Value}").ToArray(),
            string.Join(" | ", headers.Select(static field => $"{field.Name}: {field.Value}")));
        CollectionAssert.AreEqual((byte[])[0, 0, 0, 0, 7, .. HelloWorld], data);
        Assert.AreEqual(("example.com", 50051, false), (call.Connector.Target!.Host, call.Connector.Target.Port, call.Connector.Target.UseTls));
        CollectionAssert.AreEqual(new[] { "h2" }, call.Connector.Target!.ApplicationProtocols!.ToArray());
        Assert.IsTrue(call.Connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_Reply_ReportsStatusVersionContentTypeAndHeaderSizeForWriteOut()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "0")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, null);

        TransferReport report = result.Report!;
        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(new Version(2, 0), report.HttpVersion);
        Assert.AreEqual(0, report.DownloadSize);
        Assert.AreEqual("application/grpc", report.ContentType);
        Assert.AreEqual("HTTP/2 200 \r\n".Length + "content-type: application/grpc\r\n".Length + "grpc-status: 0\r\n".Length + 2, report.HeaderSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorStatus_StillReportsTheResponseHead()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "5")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, null);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAgentAndHeadersGiven_SendsThemAfterTheCallDefinition()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "0")));
        var http = new Curl.Protocol.Abstractions.HttpRequestOptions { UserAgent = "probe/1", Headers = ["X-Trace: abc", "trace-bin: AQI="] };

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, null, http: http);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        (List<HeaderField> headers, _) = await SentRequestAsync(call.Connection.Sent);
        CollectionAssert.AreEqual(
            new[] { "te: trailers", "content-type: application/grpc", "user-agent: probe/1", "x-trace: abc", "trace-bin: AQI" },
            headers.Skip(4).Select(static field => $"{field.Name}: {field.Value}").ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_BinaryMetadataNotBase64_FailsWithExit2()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "0")));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, null, http: new Curl.Protocol.Abstractions.HttpRequestOptions { Headers = ["trace-bin: ***"] });

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("The value of the gRPC metadata trace-bin is not base64", result.ErrorMessage);
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
    public async Task ExecuteAsync_Grpcs_TargetsHttpsPoolSchemeSoTheHandshakeOffersAlpnH2()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)), "h2");

        await call.RunAsync("grpcs://example.com/helloworld.Greeter/SayHello", proto.GreeterPath, "{}");

        Assert.AreEqual("https", call.Connector.Target!.PoolScheme);
        Assert.IsTrue(call.Connector.Target.IsNeverPooled);
        CollectionAssert.AreEqual(new[] { "h2" }, call.Connector.Target.ApplicationProtocols!.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Grpc_TargetsNoPoolScheme()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)));

        await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.IsNull(call.Connector.Target!.PoolScheme);
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
    [DataRow(Http2ErrorCode.NoError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.ProtocolError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.InternalError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.FlowControlError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.SettingsTimeout, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.FrameSizeError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.RefusedStream, 14, "UNAVAILABLE")]
    [DataRow(Http2ErrorCode.Cancel, 1, "CANCELLED")]
    [DataRow(Http2ErrorCode.CompressionError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.ConnectError, 13, "INTERNAL")]
    [DataRow(Http2ErrorCode.EnhanceYourCalm, 8, "RESOURCE_EXHAUSTED")]
    [DataRow(Http2ErrorCode.InadequateSecurity, 7, "PERMISSION_DENIED")]
    public async Task ExecuteAsync_RstStream_FailsWithExit22AndTheStatusItsErrorCodeMapsTo(Http2ErrorCode errorCode, int code, string name)
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(Http2FrameFactory.CreateRstStream(1, errorCode)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual($"grpc-status {code} ({name}): HTTP/2 stream 1 was reset by the peer: {errorCode}.", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(Http2ErrorCode.NoError)]
    [DataRow(Http2ErrorCode.InternalError)]
    public async Task ExecuteAsync_GoAwayBeforeTheCallsStream_FailsWithExit22AndUnavailable(Http2ErrorCode errorCode)
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(Http2FrameFactory.CreateGoAway(0, errorCode, ReadOnlyMemory<byte>.Empty)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{}");

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual($"grpc-status 14 (UNAVAILABLE): HTTP/2 GOAWAY from the peer: {errorCode}, last stream 0.", result.ErrorMessage);
    }
    [TestMethod]
    public async Task ExecuteAsync_Http2Failures_FailWithExit16()
    {
        byte[][] replies =
        [
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
    public async Task ExecuteAsync_WithoutGrpcProto_LearnsTheTypesByServerReflectionThenCallsOnStream3()
    {
        byte[] reply = Reflection.ReflectionFixture.Server(
            Reflection.ReflectionFixture.Headers(1, false, GrpcServerReply.OkHeaders),
            Reflection.ReflectionFixture.Message(1, Reflection.ReflectionFixture.FilesResponse(Reflection.ReflectionFixture.HelloWorldFile)),
            Reflection.ReflectionFixture.Trailers(1, 0),
            Reflection.ReflectionFixture.Headers(3, false, GrpcServerReply.OkHeaders),
            Reflection.ReflectionFixture.Message(3, ReplyHi),
            Reflection.ReflectionFixture.Trailers(3, 0));
        var connection = new ScriptedConnection(reply);
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = output, PostData = Encoding.UTF8.GetBytes("{\"name\":\"World\"}") };

        TransferResult result = await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection, null))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(HiJson, Encoding.UTF8.GetString(output.ToArray()));
        List<byte[]> call = await Reflection.ReflectionFixture.SentMessagesAsync(connection.Sent, 3);
        CollectionAssert.AreEqual(HelloWorld, call.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutGrpcProtoOnAServerWithoutReflection_FailsWithExit22NamingGrpcProto()
    {
        byte[] reply = GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "12"));
        var context = new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream() };

        TransferResult result = await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(new ScriptedConnection(reply), null))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The server did not answer gRPC server reflection (grpc-status 12 (UNIMPLEMENTED): ); give the method's .proto file with --grpc-proto", result.ErrorMessage);
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
    [TestMethod]
    public async Task ExecuteAsync_UploadWithGrpcProto_SendsTheFileAsTheRequestMessage()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, null, upload: "{\"name\":\"zed\"}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        (_, byte[] data) = await SentRequestAsync(call.Connection.Sent);
        CollectionAssert.AreEqual((byte[])[0, 0, 0, 0, 5, 0x0A, 0x03, 0x7A, 0x65, 0x64], data);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataAndUpload_SendsTheDataNotTheFile()
    {
        using var proto = new ProtoFixture();
        var call = new Call(GrpcServerReply.Of(GrpcServerReply.Headers(false, GrpcServerReply.OkHeaders), GrpcServerReply.Message(ReplyHi), GrpcServerReply.Trailers(0)));

        TransferResult result = await call.RunAsync(GreeterUrl, proto.GreeterPath, "{\"name\":\"zed\"}", upload: "{\"name\":\"other\"}");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        (_, byte[] data) = await SentRequestAsync(call.Connection.Sent);
        CollectionAssert.AreEqual((byte[])[0, 0, 0, 0, 5, 0x0A, 0x03, 0x7A, 0x65, 0x64], data);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithoutGrpcProto_SendsTheFileAsTheRequestMessageAfterReflection()
    {
        byte[] reply = Reflection.ReflectionFixture.Server(
            Reflection.ReflectionFixture.Headers(1, false, GrpcServerReply.OkHeaders),
            Reflection.ReflectionFixture.Message(1, Reflection.ReflectionFixture.FilesResponse(Reflection.ReflectionFixture.HelloWorldFile)),
            Reflection.ReflectionFixture.Trailers(1, 0),
            Reflection.ReflectionFixture.Headers(3, false, GrpcServerReply.OkHeaders),
            Reflection.ReflectionFixture.Message(3, ReplyHi),
            Reflection.ReflectionFixture.Trailers(3, 0));
        var connection = new ScriptedConnection(reply);
        var context = new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream(), Upload = new MemoryStream(Encoding.UTF8.GetBytes("{\"name\":\"World\"}")) };

        TransferResult result = await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection, null))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        List<byte[]> call = await Reflection.ReflectionFixture.SentMessagesAsync(connection.Sent, 3);
        CollectionAssert.AreEqual(HelloWorld, call.Single());
    }

    private static IOException Reset() => new("reset", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset));

    [TestMethod]
    public async Task ExecuteAsync_ResetWhileReadingWithGrpcProto_FailsWithExit56()
    {
        using var proto = new ProtoFixture();
        var connection = new ScriptedConnection([], readFailure: Reset());
        var context = new GrpcProtoContext(new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream(), PostData = Encoding.UTF8.GetBytes("{}") }, proto.GreeterPath);

        TransferResult result = await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection, null))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetWhileReadingReflection_FailsWithExit56()
    {
        var connection = new ScriptedConnection([], readFailure: Reset());
        var context = new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream(), PostData = Encoding.UTF8.GetBytes("{}") };

        TransferResult result = await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection, null))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetWhileWritingTheRequest_FailsWithExit55()
    {
        using var proto = new ProtoFixture();
        var connection = new ScriptedConnection([], writeFailure: Reset());
        var context = new GrpcProtoContext(new TransferContext { Url = CurlUrl.Parse(GreeterUrl), Output = new MemoryStream(), PostData = Encoding.UTF8.GetBytes("{}") }, proto.GreeterPath);

        TransferResult result = await new GrpcProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection, null))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", result.ErrorMessage);
    }

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

        public async Task<TransferResult> RunAsync(string url, string protoPath, string? data, TimeSpan? maxTime = null, Curl.Protocol.Abstractions.HttpRequestOptions? http = null, string? upload = null)
        {
            var connector = new RecordingConnector(this, applicationProtocol);
            var context = new TransferContext
            {
                Url = CurlUrl.Parse(url),
                Output = output,
                PostData = data is null ? default(ReadOnlyMemory<byte>?) : Encoding.UTF8.GetBytes(data),
                Upload = upload is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(upload)),
                MaxTime = maxTime,
                Http = http,
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
