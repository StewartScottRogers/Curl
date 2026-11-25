using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the HTTP version option's mapping onto the handler's preference and the ALPN offer, as
/// ADR-0141 measured each platform's curl (<c>curl -v https://www.google.com/</c>): the
/// Schannel build offers <c>http/1.1</c> by default, the nghttp2 builds <c>h2,http/1.1</c>.
/// </summary>
[TestClass]
public sealed class HttpVersionMappingTests
{
    private const string Url = "https://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(HttpVersion.None, HttpVersionPreference.Http11)]
    [DataRow(HttpVersion.Http10, HttpVersionPreference.Http10)]
    [DataRow(HttpVersion.Http11, HttpVersionPreference.Http11)]
    [DataRow(HttpVersion.Http2, HttpVersionPreference.Http2)]
    [DataRow(HttpVersion.Http2PriorKnowledge, HttpVersionPreference.Http2PriorKnowledge)]
    [DataRow(HttpVersion.Http3, HttpVersionPreference.Http3)]
    [DataRow(HttpVersion.Http3Only, HttpVersionPreference.Http3Only)]
    [DataRow((HttpVersion)99, HttpVersionPreference.Http11)]
    public void ToHttpVersionPreference_MapsEachVersionOption(HttpVersion version, HttpVersionPreference expected)
    {
        Diagnostics.Arrange("version", version);

        HttpVersionPreference actual = HttpVersionMapping.ToHttpVersionPreference(version);

        Diagnostics.Act("preference", actual);

        Diagnostics.Assert("preference", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(HttpVersion.None, "h2,http/1.1")]
    [DataRow(HttpVersion.Http10, "http/1.1")]
    [DataRow(HttpVersion.Http11, "http/1.1")]
    [DataRow(HttpVersion.Http2, "h2,http/1.1")]
    [DataRow(HttpVersion.Http2PriorKnowledge, "h2")]
    [DataRow(HttpVersion.Http3, "h2,http/1.1")]
    [DataRow(HttpVersion.Http3Only, "h2,http/1.1")]
    public void HttpOverTlsApplicationProtocolsOf_OffersWhatThePlatformsCurlOffers(HttpVersion version, string expected)
    {
        Diagnostics.Arrange("version", version);

        string actual = string.Join(',', HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(version));

        Diagnostics.Act("offered", actual);

        Diagnostics.Assert("offered", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CreateTcpConnector_NoVersionOption_OffersH2ThenHttp11()
    {
        Diagnostics.Arrange("arguments", Url);

        string actual = OfferedBy(Url);

        Diagnostics.Act("offered", actual);

        Diagnostics.Assert("offered", "h2,http/1.1", actual);
        Assert.AreEqual("h2,http/1.1", actual);
    }

    [TestMethod]
    [DataRow("--http1.0", "http/1.1")]
    [DataRow("--http1.1", "http/1.1")]
    [DataRow("--http2", "h2,http/1.1")]
    [DataRow("--http2-prior-knowledge", "h2")]
    [DataRow("--http3", "h2,http/1.1")]
    [DataRow("--http3-only", "h2,http/1.1")]
    public void CreateTcpConnector_VersionOption_OffersTheSameOnEveryPlatform(string option, string expected)
    {
        Diagnostics.Arrange("option", option);

        string actual = OfferedBy(option, Url);

        Diagnostics.Act("offered", actual);

        Diagnostics.Assert("offered", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("--http2", HttpVersionPreference.Http2)]
    [DataRow("--http2-prior-knowledge", HttpVersionPreference.Http2PriorKnowledge)]
    [DataRow("--http3", HttpVersionPreference.Http3)]
    [DataRow("--http3-only", HttpVersionPreference.Http3Only)]
    public void HttpRequestOptionsFromCommandLine_Http2OrHttp3Option_SetsTheVersion(string option, HttpVersionPreference expected)
    {
        Diagnostics.Arrange("option", option);
        CommandLineParseResult result = QuicBuildParser.Parse([option, Url]);
        Assert.IsTrue(result.IsAccepted);

        HttpVersionPreference actual = HttpRequestOptionsMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(result.Options), EasyOptionsFromCommandLine.HttpOf(result.Options), new EasyBehaviourOptions(), new EasyTransferOptions(), new EasyAuthenticationOptions()).Version;

        Diagnostics.Act("version", actual);

        Diagnostics.Assert("version", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private static string OfferedBy(params string[] arguments)
    {
        CommandLineParseResult result = QuicBuildParser.Parse(arguments);
        Assert.IsTrue(result.IsAccepted);

        CurlTransports transports = EngineComposition.CreateTransports(EasyOptionsFromCommandLine.ToolOptionsOf(result.Options), EasyOptionsFromCommandLine.NetworkOf(result.Options), EasyOptionsFromCommandLine.TlsOf(result.Options), EasyOptionsFromCommandLine.HttpOf(result.Options), EasyOptionsFromCommandLine.ProxyOf(result.Options), EasyOptionsFromCommandLine.AuthenticationOf(result.Options), TimeProvider.System, traces: EasyOptionsFromCommandLine.TraceComponentsOf(result.Options));

        return string.Join(',', transports.TcpConnector.HttpOverTlsApplicationProtocols);
    }
}
