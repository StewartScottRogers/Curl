using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins curl 8.18.0's failure, with libidn2 2.3.8, of a transfer through an HTTP proxy to a host
/// whose UTS 46 mapping holds a slash (<c>a℀b.test</c> is <c>aa/cb.test</c>): once the proxy
/// connection is open it cannot build the absolute-form target, exits 27 with <c>Out of memory</c>
/// and sends the proxy nothing (measured, BL-4077 and BL-4080). On Windows the host is converted
/// from its bytes read as Windows-1252, which leaves no slash, so the transfer tests run elsewhere.
/// </summary>
[TestClass]
public sealed class HttpProtocolHandlerAbsoluteFormHostTests
{
    private const string OkResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("aa/cb.test", DisplayName = "slash")]
    [DataRow("a,b.test", DisplayName = "comma")]
    [DataRow("a b.test", DisplayName = "space")]
    public void RefusesAbsoluteFormHost_HostHoldingACharacterUrlSetRefuses_IsTrue(string wireHost)
    {
        bool refused = HttpProtocolHandler.RefusesAbsoluteFormHost(wireHost);

        Assert.IsTrue(refused);
    }

    [TestMethod]
    [DataRow("example.com", DisplayName = "name")]
    [DataRow("a_b.test", DisplayName = "low line")]
    [DataRow("xn--9ca.test", DisplayName = "punycode")]
    [DataRow("[fe80::1%25eth0]", DisplayName = "IPv6 literal with a zone")]
    public void RefusesAbsoluteFormHost_HostCurlAccepts_IsFalse(string wireHost)
    {
        bool refused = HttpProtocolHandler.RefusesAbsoluteFormHost(wireHost);

        Assert.IsFalse(refused);
    }

    [TestMethod]
    [DataRow("http://a℀b.test/", DisplayName = "account of, aa/cb.test")]
    [DataRow("http://a℆b.test/", DisplayName = "cada una, ac/ub.test")]
    public async Task ExecuteAsync_ProxiedHostHoldingASlash_FailsWithOutOfMemoryAndSendsNothing(string url)
    {
        using IDisposable utf8Host = CurlUrlHost.ReadingHostAs(hostAsWindows1252: false);
        (TransferResult result, string written) = await RunThroughProxyAsync(url);

        Assert.AreEqual(CurlExitCode.OutOfMemory, result.ExitCode);
        Assert.AreEqual("Out of memory", result.ErrorMessage);
        Assert.IsTrue(result.Report!.UsedProxy);
        Assert.AreEqual(string.Empty, written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxiedIpv6Literal_SendsTheAbsoluteFormRequest()
    {
        (TransferResult result, string written) = await RunThroughProxyAsync("http://[::1]:8080/");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.StartsWith("GET http://[::1]:8080/ HTTP/1.1\r\n", written);
    }

    /// <summary>
    /// Runs one transfer of <paramref name="url" /> through an HTTP proxy that would answer a 200,
    /// and gives the result and the bytes sent to the proxy.
    /// </summary>
    private async Task<(TransferResult Result, string Written)> RunThroughProxyAsync(string url)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TurnTakingConnection connection = new(65536, OkResponse);
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18183, null) };
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Http = options, Events = new RecordingTransferEvents() };
        diagnostics.Arrange("url, wire host", $"{url}, {context.Url.IdnHost}");
        HttpProtocolHandler handler = new(QueueConnector.For(connection), new BasicAndBearerAuthenticator(Encoding.UTF8));

        TransferResult result = await handler.ExecuteAsync(context);

        diagnostics.Act("exit, request", $"{result.ExitCode}, {connection.Written}");
        return (result, connection.Written);
    }
}
