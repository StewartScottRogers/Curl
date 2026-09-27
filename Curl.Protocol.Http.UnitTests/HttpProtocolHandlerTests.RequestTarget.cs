using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s request line under <c>--request-target</c>
/// and <c>--path-as-is</c> through <see cref="TurnTakingConnection" />, never a socket. Each
/// request is what curl 8.21.0 sent a loopback server; the commands are in the BL-186 Notes.
/// Each exchange is replayed with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string TargetResponse = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    /// <summary>
    /// Measured: <c>curl -X OPTIONS --request-target '*' http://127.0.0.1:18186/a/b?q=1</c>
    /// sends <c>OPTIONS * HTTP/1.1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_RequestTargetStarWithOptions_SendsTheMeasuredRequestLine()
    {
        HttpRequestOptions options = new() { CustomMethod = "OPTIONS", RequestTarget = "*" };

        await AssertRequestAsync(
            "OPTIONS * HTTP/1.1\r\nHost: 127.0.0.1:18186\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            CurlUrl.Parse("http://127.0.0.1:18186/a/b?q=1"),
            options);
    }

    /// <summary>
    /// Measured: <c>curl --path-as-is http://127.0.0.1:18187/a/../b</c> sends
    /// <c>GET /a/../b HTTP/1.1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PathAsIsDotSegments_SendsThemUnsquashed()
    {
        await AssertRequestAsync(
            "GET /a/../b HTTP/1.1\r\nHost: 127.0.0.1:18187\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            CurlUrl.Parse("http://127.0.0.1:18187/a/../b", pathAsIs: true),
            null);
    }

    /// <summary>
    /// Measured: <c>curl http://127.0.0.1:18188/a/../b</c> sends <c>GET /b HTTP/1.1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DotSegmentsWithoutPathAsIs_SendsThemSquashed()
    {
        await AssertRequestAsync(
            "GET /b HTTP/1.1\r\nHost: 127.0.0.1:18188\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            CurlUrl.Parse("http://127.0.0.1:18188/a/../b"),
            null);
    }

    [TestMethod]
    public async Task ExecuteAsync_RequestTarget_IsTheTargetTheAuthenticatorIsAskedAbout()
    {
        ScriptedAuthenticator authenticator = new(null, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18189/a"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { RequestTarget = "/x/../y?z" },
        };

        await new HttpProtocolHandler(QueueConnector.For(new TurnTakingConnection(65536, TargetResponse)), authenticator).ExecuteAsync(context);

        Assert.AreEqual("/x/../y?z", authenticator.Calls[0].Request.RequestTarget);
    }

    private static async Task AssertRequestAsync(string expected, CurlUrl url, HttpRequestOptions? options)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, TargetResponse);
            TransferContext context = new() { Url = url, Output = new MemoryStream(), Http = options };

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expected, connection.Written, $"Chunk size {chunkSize}");
        }
    }
}
