using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Pins which <c>ftp</c> transfers <see cref="ForwardedFtpProtocolHandler" /> hands to the HTTP
/// handler (ADR-0056, rule 3): those through an HTTP or HTTP/1.0 proxy without <c>-p</c>, and no
/// other.
/// </summary>
[TestClass]
public sealed class ForwardedFtpProtocolHandlerTests
{
    [TestMethod]
    [DataRow(ProxyKind.Http)]
    [DataRow(ProxyKind.Http10)]
    public async Task ExecuteAsync_HttpProxyWithoutProxyTunnel_IsPerformedByTheHttpHandler(ProxyKind kind)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        TransferContext context = ContextWith(new HttpRequestOptions { ForwardProxy = ProxyOf(kind) });

        TransferResult result = await new ForwardedFtpProtocolHandler(http).ExecuteAsync(context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(context, http.Contexts.Single());
    }

    [TestMethod]
    [DataRow(ProxyKind.Http, true)]
    [DataRow(ProxyKind.Http10, true)]
    [DataRow(ProxyKind.Https, false)]
    [DataRow(ProxyKind.Socks5, false)]
    public async Task ExecuteAsync_ProxyThatIsNotForwardedThrough_FailsAsAnUnsupportedProtocol(ProxyKind kind, bool proxyTunnel)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");
        TransferContext context = ContextWith(new HttpRequestOptions { ForwardProxy = ProxyOf(kind), ProxyTunnel = proxyTunnel });

        TransferResult result = await new ForwardedFtpProtocolHandler(http).ExecuteAsync(context);

        AssertUnsupported(result);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoProxy_FailsAsAnUnsupportedProtocol()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        TransferResult result = await new ForwardedFtpProtocolHandler(http).ExecuteAsync(ContextWith(new HttpRequestOptions()));

        AssertUnsupported(result);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoHttpOptions_FailsAsAnUnsupportedProtocol()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

        TransferResult result = await new ForwardedFtpProtocolHandler(http).ExecuteAsync(ContextWith(null));

        AssertUnsupported(result);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public void SupportedSchemes_IsFtpOnly()
    {
        ForwardedFtpProtocolHandler handler = new(RecordingProtocolHandler.WritingPath("http"));

        CollectionAssert.AreEqual(new[] { "ftp" }, handler.SupportedSchemes.ToArray());
    }

    private static void AssertUnsupported(TransferResult result)
    {
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"ftp\" not supported", result.ErrorMessage);
    }

    private static ProxyEndpoint ProxyOf(ProxyKind kind) => new(kind, "127.0.0.1", 1, null);

    private static TransferContext ContextWith(HttpRequestOptions? http) =>
        new()
        {
            Url = CurlUrl.Parse("ftp://example.com/f.txt"),
            Output = new MemoryStream(),
            Http = http,
        };
}
