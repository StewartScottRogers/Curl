using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the request bytes and <c>-v</c> lines for <c>-u :</c> and <c>-U :</c>, an empty user
/// with an empty password, as curl 8.22.0 sent them to a loopback server (measured with
/// <c>Record-CurlExchange.ps1</c>, BL-3176 Notes): no <c>Authorization</c> or
/// <c>Proxy-Authorization</c> header and no <c>Server auth</c> or <c>Proxy auth</c> line.
/// </summary>
[TestClass]
public sealed class HttpProtocolHandlerEmptyCredentialsTests
{
    private const string OkResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_EmptyUserAndPassword_SendsNoAuthorizationAndWritesNoServerAuthLine()
    {
        (string request, List<string> events) = await RunAsync("http://127.0.0.1:18183/a", new HttpRequestOptions(), new NetworkCredential(string.Empty, string.Empty));

        Assert.AreEqual("GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n", request);
        Assert.DoesNotContain("authorization:", request.ToLowerInvariant());
        Assert.IsFalse(events.Any(line => line.Contains("Server auth", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyProxyUserAndPassword_SendsNoProxyAuthorizationAndWritesNoProxyAuthLine()
    {
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18183, new NetworkCredential(string.Empty, string.Empty)) };

        (string request, List<string> events) = await RunAsync("http://example.invalid/", options, credential: null);

        Assert.StartsWith("GET http://example.invalid/ HTTP/1.1\r\n", request);
        Assert.DoesNotContain("authorization:", request.ToLowerInvariant());
        Assert.IsFalse(events.Any(line => line.Contains("Proxy auth", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyUserWithEmptyPassword_SendsTheMeasuredProxyAuthorization()
    {
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18183, new NetworkCredential("user", string.Empty)) };

        (string request, List<string> events) = await RunAsync("http://example.invalid/", options, credential: null);

        StringAssert.Contains(request, "\r\nProxy-Authorization: Basic dXNlcjo=\r\n");
        CollectionAssert.Contains(events, "* Proxy auth using Basic with user 'user'");
    }

    /// <summary>
    /// Runs one <c>-v</c> transfer of <paramref name="url" /> answered by a 200 and gives the
    /// request bytes sent and the events written.
    /// </summary>
    private async Task<(string Request, List<string> Events)> RunAsync(string url, HttpRequestOptions options, NetworkCredential? credential)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TurnTakingConnection connection = new(65536, OkResponse);
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credential, Http = options, Events = events };
        diagnostics.Arrange("url, user", $"{url}, {credential?.UserName ?? "(none)"}");
        HttpProtocolHandler handler = new(QueueConnector.For(connection), new BasicAndBearerAuthenticator(Encoding.UTF8));

        TransferResult result = await handler.ExecuteAsync(context);

        diagnostics.Act("request", connection.Written);
        diagnostics.Act("events", string.Join(Environment.NewLine, events.Events));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return (connection.Written, [.. events.Events]);
    }
}
