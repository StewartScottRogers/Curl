using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Checks that <c>-u user:password</c> reaches the server as an <c>authorization: Basic</c> request header (BL-5296).</summary>
[TestClass]
public sealed class GrpcBasicCredentialsTests
{
    private const string GreeterUrl = "grpc://example.com:50051/helloworld.Greeter/SayHello";

    [TestMethod]
    public async Task ExecuteAsync_LoginGiven_SendsAuthorizationBasic()
    {
        List<string> headers = await SentHeadersAsync(new System.Net.NetworkCredential("user", "password"), null);

        CollectionAssert.Contains(headers, "authorization: Basic dXNlcjpwYXNzd29yZA==");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoLogin_SendsNoAuthorization()
    {
        List<string> headers = await SentHeadersAsync(null, null);

        Assert.IsFalse(headers.Any(static header => header.StartsWith("authorization:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginAndAuthorizationHeaderGiven_SendsOnlyTheHeader()
    {
        List<string> headers = await SentHeadersAsync(new System.Net.NetworkCredential("user", "password"), ["Authorization: Bearer t"]);

        CollectionAssert.AreEqual(new[] { "authorization: Bearer t" }, headers.Where(static header => header.StartsWith("authorization:", StringComparison.Ordinal)).ToArray());
    }

    private static async Task<List<string>> SentHeadersAsync(System.Net.NetworkCredential? login, string[]? customHeaders)
    {
        using var proto = new ProtoFixture();
        byte[] reply = GrpcServerReply.Of(GrpcServerReply.Headers(true, ":status", "200", "content-type", "application/grpc", "grpc-status", "0"));
        var connection = new ScriptedConnection(reply);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(GreeterUrl),
            Output = new MemoryStream(),
            Credentials = login,
            Http = customHeaders is null ? null : new Curl.Protocol.Abstractions.HttpRequestOptions { Headers = customHeaders },
        };

        TransferResult result = await new GrpcProtocolHandler(new ConnectionConnector(connection)).ExecuteAsync(new GrpcProtoContext(context, proto.GreeterPath));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        byte[] sent = connection.Sent;
        using var stream = new MemoryStream(sent, Http2Connection.ClientPreface.Length, sent.Length - Http2Connection.ClientPreface.Length);
        List<string> headers = [];
        while (await Http2FrameCodec.ReadAsync(stream, 1 << 20, CancellationToken.None) is { } frame)
        {
            if (frame.StreamId == 1 && frame.Type == Http2FrameType.Headers)
            {
                headers.AddRange(new HpackDecoder().Decode(frame.Payload.Span).Select(static field => $"{field.Name}: {field.Value}"));
            }
        }

        return headers;
    }

    private sealed class ConnectionConnector(ScriptedConnection connection) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(connection, null));
    }
}
