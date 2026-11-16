using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--http3</c> and <c>--http3-only</c> as accepted on every platform and <c>curl -V</c> listing
/// <c>HTTP3</c> (ADR-1282, superseding ADR-0540).
/// </summary>
[TestClass]
public sealed class Http3OptionsAcceptedTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--http3", RequestedHttpVersion.Http3, false)]
    [DataRow("--http3-only", RequestedHttpVersion.Http3Only, false)]
    [DataRow("--http3", RequestedHttpVersion.Http3, true)]
    [DataRow("--http3-only", RequestedHttpVersion.Http3Only, true)]
    public void Parse_Http3Option_IsAcceptedAndSelectsTheVersion(string spelling, RequestedHttpVersion expected, bool isWindows)
    {
        Diagnostics.ArrangeArguments([spelling, Url]);

        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url], _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows);

        Diagnostics.ActParse(result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.HttpVersion);
    }

    [TestMethod]
    public void Parse_Http3OptionThroughTheDefaultConfigFileSearchOverload_IsAccepted()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-q", "--http3", Url], _ => false, new EmptyPasswordPrompt(), new RecordingDataFileReader(), new DefaultConfigFileSearch(_ => null, isWindows: false, null, null), isWindows: false);

        Diagnostics.ActParse(result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http3, result.Options.HttpVersion);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Lines_FeaturesLine_ListsHttp3(bool isWindows)
    {
        string features = CurlVersionText.Lines(isWindows, isMacOS: false)[3];

        StringAssert.Contains(features, " HTTP2 HTTP3 HTTPS-proxy ");
    }

    [TestMethod]
    [DataRow("## --http3\n")]
    [DataRow("## --http3-only\n")]
    public void AiHelp_SectionOfAnHttp3Option_SaysNothingAboutRefusal(string heading)
    {
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("all", out string markdown));
        int start = markdown.IndexOf(heading, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];

        Assert.DoesNotContain("refused", section);
    }
}
