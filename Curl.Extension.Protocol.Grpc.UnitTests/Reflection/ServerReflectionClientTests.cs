using Curl.Extension.Protocol.Grpc.Client;
using Curl.Extension.Protocol.Grpc.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Protocol.Grpc.Reflection.ReflectionFixture;

namespace Curl.Extension.Protocol.Grpc.Reflection;

/// <summary>
/// Checks server reflection v1 over a scripted connection: the <c>ServerReflectionInfo</c> stream's requests, following
/// dependencies, and every way the server can fail to describe the method.
/// </summary>
[TestClass]
public sealed class ServerReflectionClientTests
{
    private static readonly GrpcRequestTarget Greeter = new("http", "example.com:50051", "helloworld.Greeter", "SayHello");

    private static readonly byte[] MessagesFile = File(
        "helloworld/messages.proto",
        "helloworld",
        "proto3",
        [],
        [Message("HelloRequest", [Field("name", 1, 1, 9)]), Message("HelloReply", [Field("message", 1, 1, 9)])],
        [],
        []);

    private static readonly byte[] GreeterFile = File(
        "helloworld/greeter.proto",
        "helloworld",
        "proto3",
        ["helloworld/messages.proto"],
        [],
        [],
        [Service("Greeter", [("SayHello", ".helloworld.HelloRequest", ".helloworld.HelloReply")])]);

    [TestMethod]
    public async Task DescribeAsync_FileWithDependency_AsksForTheSymbolThenTheDependencyAndGivesTheTypes()
    {
        (GrpcHttp2Session session, ScriptedConnection connection) = await SessionAsync(Server(
            Headers(1, false, OkHeaders), Message(1, FilesResponse(GreeterFile)), Message(1, FilesResponse(MessagesFile)), Trailers(1, 0)));

        GrpcProtoMethod method = await ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None);

        Assert.AreEqual(("helloworld.HelloRequest", "helloworld.HelloReply"), (method.Input.FullName, method.Output.FullName));
        List<byte[]> sent = await SentMessagesAsync(connection.Sent, 1);
        Assert.HasCount(2, sent);
        CollectionAssert.AreEqual(ServerReflectionRequest.FileContainingSymbol("helloworld.Greeter"), sent[0]);
        CollectionAssert.AreEqual(ServerReflectionRequest.FileByFilename("helloworld/messages.proto"), sent[1]);
    }

    [TestMethod]
    public async Task DescribeAsync_Always_OpensTheReflectionMethodAndEndsItsHalfOfTheStream()
    {
        (GrpcHttp2Session session, ScriptedConnection connection) = await SessionAsync(Server(
            Headers(1, false, OkHeaders), Message(1, FilesResponse(HelloWorldFile)), Message(1, FilesResponse()), Trailers(1, 0)));

        await ServerReflectionClient.DescribeAsync(session, Greeter, TimeSpan.FromSeconds(1), CancellationToken.None);

        (List<HeaderField> headers, bool isEndStreamSent) = await SentStreamAsync(connection.Sent);
        CollectionAssert.AreEqual(
            new[] { ":method: POST", ":scheme: http", ":path: /grpc.reflection.v1.ServerReflection/ServerReflectionInfo", ":authority: example.com:50051", "te: trailers", "grpc-timeout: 1000000u", "content-type: application/grpc" },
            headers.Select(static field => $"{field.Name}: {field.Value}").ToArray());
        Assert.IsTrue(isEndStreamSent);
    }

    [TestMethod]
    public async Task DescribeAsync_ServerWithoutReflection_FailsWithExit22NamingGrpcProto()
    {
        (GrpcHttp2Session session, _) = await SessionAsync(Server(
            Headers(1, true, ":status", "200", "content-type", "application/grpc", "grpc-status", "12", "grpc-message", "unknown service grpc.reflection.v1.ServerReflection")));

        GrpcClientException failure = await Assert.ThrowsExactlyAsync<GrpcClientException>(() => ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, failure.ExitCode);
        Assert.AreEqual(
            "The server did not answer gRPC server reflection (grpc-status 12 (UNIMPLEMENTED): unknown service grpc.reflection.v1.ServerReflection); give the method's .proto file with --grpc-proto",
            failure.Message);
    }

    [TestMethod]
    public async Task DescribeAsync_ErrorResponse_FailsWithExit22NamingGrpcProto()
    {
        (GrpcHttp2Session session, _) = await SessionAsync(Server(Headers(1, false, OkHeaders), Message(1, ErrorResponse(5, "symbol not found: helloworld.Greeter"))));

        GrpcClientException failure = await Assert.ThrowsExactlyAsync<GrpcClientException>(() => ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, failure.ExitCode);
        Assert.AreEqual("gRPC server reflection failed (error 5: symbol not found: helloworld.Greeter); give the method's .proto file with --grpc-proto", failure.Message);
    }

    [TestMethod]
    public async Task DescribeAsync_ConnectionClosedBeforeAnAnswer_FailsWithExit16()
    {
        (GrpcHttp2Session session, _) = await SessionAsync(Server(Headers(1, false, OkHeaders)));

        GrpcClientException failure = await Assert.ThrowsExactlyAsync<GrpcClientException>(() => ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
        Assert.AreEqual("The server closed the HTTP/2 connection during gRPC server reflection", failure.Message);
    }

    [TestMethod]
    public async Task DescribeAsync_WindowTooSmallForTheDependencyRequest_FailsWithExit16()
    {
        (GrpcHttp2Session session, _) = await SessionAsync(Server(
            [new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 0)], Headers(1, false, OkHeaders), Message(1, FilesResponse(GreeterFile))));

        GrpcClientException failure = await Assert.ThrowsExactlyAsync<GrpcClientException>(() => ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.Http2, failure.ExitCode);
        Assert.AreEqual("The server's HTTP/2 flow-control window did not take a server reflection request", failure.Message);
    }

    [TestMethod]
    public async Task DescribeAsync_DependencyReplyWithoutThatFile_ThrowsGrpcWireException()
    {
        (GrpcHttp2Session session, _) = await SessionAsync(Server(Headers(1, false, OkHeaders), Message(1, FilesResponse(GreeterFile)), Message(1, FilesResponse(HelloWorldFile))));

        GrpcWireException failure = await Assert.ThrowsExactlyAsync<GrpcWireException>(() => ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None));

        Assert.AreEqual("The server's reflection reply for helloworld/messages.proto did not hold that file.", failure.Message);
    }

    [TestMethod]
    [DataRow("other.Greeter", "SayHello", "gRPC server reflection: the server's files declare no service other.Greeter")]
    [DataRow("helloworld.Greeter", "SayBye", "gRPC server reflection: service helloworld.Greeter has no method SayBye")]
    public async Task DescribeAsync_NoSuchServiceOrMethod_FailsWithExit2(string service, string method, string expected)
    {
        (GrpcHttp2Session session, _) = await SessionAsync(Server(Headers(1, false, OkHeaders), Message(1, FilesResponse(HelloWorldFile)), Trailers(1, 0)));

        GrpcClientException failure = await Assert.ThrowsExactlyAsync<GrpcClientException>(() => ServerReflectionClient.DescribeAsync(session, Greeter with { Service = service, Method = method }, null, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(expected, failure.Message);
    }

    [TestMethod]
    public async Task DescribeAsync_ServiceWithoutPackage_IsNamedByItsNameAlone()
    {
        byte[] file = File("g.proto", string.Empty, "proto3", [], [Message("Empty", [])], [], [Service("Pinger", [("Ping", ".Empty", ".Empty")])]);
        (GrpcHttp2Session session, _) = await SessionAsync(Server(Headers(1, false, OkHeaders), Message(1, FilesResponse(file)), Trailers(1, 0)));

        GrpcProtoMethod method = await ServerReflectionClient.DescribeAsync(session, Greeter with { Service = "Pinger", Method = "Ping" }, null, CancellationToken.None);

        Assert.AreEqual(("Empty", "Empty"), (method.Input.FullName, method.Output.FullName));
    }

    [TestMethod]
    public async Task DescribeAsync_TypeNoFileDeclares_ThrowsGrpcWireException()
    {
        byte[] file = File("g.proto", "helloworld", "proto3", [], [], [], [Service("Greeter", [("SayHello", ".helloworld.Missing", ".helloworld.Missing")])]);
        (GrpcHttp2Session session, _) = await SessionAsync(Server(Headers(1, false, OkHeaders), Message(1, FilesResponse(file)), Trailers(1, 0)));

        GrpcWireException failure = await Assert.ThrowsExactlyAsync<GrpcWireException>(() => ServerReflectionClient.DescribeAsync(session, Greeter, null, CancellationToken.None));

        Assert.StartsWith("The server's reflection files do not resolve: No message is named .helloworld.Missing.", failure.Message);
    }

    /// <summary>Reads the header fields the client sent on stream 1, and whether it ended its half of the stream.</summary>
    private static async Task<(List<HeaderField> Headers, bool IsEndStreamSent)> SentStreamAsync(byte[] sent)
    {
        using var stream = new MemoryStream(sent, Http2Connection.ClientPreface.Length, sent.Length - Http2Connection.ClientPreface.Length);
        List<HeaderField> headers = [];
        bool isEndStreamSent = false;
        while (await Http2FrameCodec.ReadAsync(stream, 1 << 20, CancellationToken.None) is { } frame)
        {
            if (frame.StreamId == 1 && frame.Type == Http2FrameType.Headers)
            {
                headers.AddRange(new HpackDecoder().Decode(frame.Payload.Span));
            }

            isEndStreamSent |= frame.StreamId == 1 && frame.Type == Http2FrameType.Data && (frame.Flags & Http2FrameFlags.EndStream) != 0;
        }

        return (headers, isEndStreamSent);
    }
}
