using Curl.Cli;
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
    [DataRow(null, HttpVersionPreference.Http11)]
    [DataRow(RequestedHttpVersion.Http10, HttpVersionPreference.Http10)]
    [DataRow(RequestedHttpVersion.Http11, HttpVersionPreference.Http11)]
    [DataRow(RequestedHttpVersion.Http2, HttpVersionPreference.Http2)]
    [DataRow(RequestedHttpVersion.Http2PriorKnowledge, HttpVersionPreference.Http2PriorKnowledge)]
    [DataRow(RequestedHttpVersion.Http3, HttpVersionPreference.Http3)]
    [DataRow(RequestedHttpVersion.Http3Only, HttpVersionPreference.Http3Only)]
    [DataRow((RequestedHttpVersion)99, HttpVersionPreference.Http11)]
    public void ToHttpVersionPreference_MapsEachVersionOption(RequestedHttpVersion? version, HttpVersionPreference expected)
    {
        Diagnostics.Arrange("version", version);

        HttpVersionPreference actual = HttpVersionMapping.ToHttpVersionPreference(version);

        Diagnostics.Act("preference", actual);

        Diagnostics.Assert("preference", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(null, true, "http/1.1")]
    [DataRow(null, false, "h2,http/1.1")]
    [DataRow(RequestedHttpVersion.Http10, true, "http/1.1")]
    [DataRow(RequestedHttpVersion.Http10, false, "http/1.1")]
    [DataRow(RequestedHttpVersion.Http11, true, "http/1.1")]
    [DataRow(RequestedHttpVersion.Http11, false, "http/1.1")]
    [DataRow(RequestedHttpVersion.Http2, true, "h2,http/1.1")]
    [DataRow(RequestedHttpVersion.Http2, false, "h2,http/1.1")]
    [DataRow(RequestedHttpVersion.Http2PriorKnowledge, true, "h2")]
    [DataRow(RequestedHttpVersion.Http2PriorKnowledge, false, "h2")]
    [DataRow(RequestedHttpVersion.Http3, true, "h2,http/1.1")]
    [DataRow(RequestedHttpVersion.Http3, false, "h2,http/1.1")]
    [DataRow(RequestedHttpVersion.Http3Only, true, "h2,http/1.1")]
    [DataRow(RequestedHttpVersion.Http3Only, false, "h2,http/1.1")]
    public void HttpOverTlsApplicationProtocolsOf_OffersWhatThePlatformsCurlOffers(RequestedHttpVersion? version, bool isWindows, string expected)
    {
        Diagnostics.Arrange("version", version);
        Diagnostics.Arrange("isWindows", isWindows);

        string actual = string.Join(',', HttpVersionMapping.HttpOverTlsApplicationProtocolsOf(version, isWindows));

        Diagnostics.Act("offered", actual);

        Diagnostics.Assert("offered", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void CreateTcpConnector_NoVersionOptionOnWindows_OffersHttp11Only()
    {
        Diagnostics.Arrange("arguments", Url);

        string actual = OfferedBy(Url);

        Diagnostics.Act("offered", actual);

        Diagnostics.Assert("offered", "http/1.1", actual);
        Assert.AreEqual("http/1.1", actual);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void CreateTcpConnector_NoVersionOptionOffWindows_OffersH2ThenHttp11()
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
        CommandLineParseResult result = OpenSslBuildParser.Parse([option, Url]);
        Assert.IsTrue(result.IsAccepted);

        HttpVersionPreference actual = HttpRequestOptionsMapping.FromCommandLine(result.Options).Version;

        Diagnostics.Act("version", actual);

        Diagnostics.Assert("version", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private static string OfferedBy(params string[] arguments)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(arguments);
        Assert.IsTrue(result.IsAccepted);

        CurlTransports transports = CurlComposition.CreateTransports(result.Options, TimeProvider.System);

        return string.Join(',', transports.TcpConnector.HttpOverTlsApplicationProtocols);
    }
}
