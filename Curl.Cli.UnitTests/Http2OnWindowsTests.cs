using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--http2</c>, <c>--http2-prior-knowledge</c> and <c>--proxy-http2</c> running on Windows over Curl's
/// own HTTP/2 (ADR-0141) instead of being refused with exit 2 as curl's Schannel build refuses them: Stewart
/// turned HTTP/2 on for Windows (ADR-0871, BL-3207), though <c>curl -V</c> lists no <c>HTTP2</c> there, as the
/// Schannel reference build lists none (ADR-0808, BL-3359).
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class Http2OnWindowsTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--http2", RequestedHttpVersion.Http2)]
    [DataRow("--http2-prior-knowledge", RequestedHttpVersion.Http2PriorKnowledge)]
    public void Parse_Http2VersionOnWindows_SelectsHttp2(string spelling, RequestedHttpVersion expected)
    {
        CommandLineParseResult result = ParseOnWindows(["-s", spelling, Url]);

        Diagnostics.Assert("http version", expected, result.IsAccepted ? result.Options.HttpVersion : null);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.HttpVersion);
    }

    [TestMethod]
    public void Parse_Http2BeforeHttp11OnWindows_TheLastVersionWins()
    {
        CommandLineParseResult result = ParseOnWindows(["--http2", "--http1.1", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http11, result.Options.HttpVersion);
    }

    [TestMethod]
    [DataRow("--proxy-http2", true)]
    [DataRow("--no-proxy-http2", false)]
    public void Parse_ProxyHttp2OnWindows_SetsProxyHttp2(string spelling, bool expected)
    {
        CommandLineParseResult result = ParseOnWindows(["-s", spelling, Url]);

        Diagnostics.Assert("proxy http2", expected, result.IsAccepted && result.Options.ProxyHttp2);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.ProxyHttp2);
    }

    [TestMethod]
    [DataRow("## --http2\n")]
    [DataRow("## --http2-prior-knowledge\n")]
    [DataRow("## --proxy-http2\n")]
    public void AiHelp_Http2OptionSection_SaysNothingAboutAWindowsRefusal(string heading)
    {
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("all", out string markdown));
        int start = markdown.IndexOf(heading, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];
        Diagnostics.Act("section", section.ReplaceLineEndings("\\n"));

        Assert.DoesNotContain("refused", section);
    }

    [TestMethod]
    public void Lines_Windows_ListsHttpsProxyRightAfterHstsWithNoHttp2()
    {
        string[] features = CurlVersionText.Lines(isWindows: true, isMacOS: false)[3].Split(' ');
        Diagnostics.Act("features", string.Join(' ', features));

        int hsts = Array.IndexOf(features, "HSTS");
        Assert.IsGreaterThanOrEqualTo(0, hsts);
        Assert.AreEqual("HTTPS-proxy", features[hsts + 1]);
        CollectionAssert.DoesNotContain(features, "HTTP2");
    }

    private CommandLineParseResult ParseOnWindows(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: true);
        Diagnostics.ActParse(result);
        return result;
    }
}
