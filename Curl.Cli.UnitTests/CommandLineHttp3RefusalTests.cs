using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--http3</c> and <c>--http3-only</c> as every reference build answers them (gap finding GF-0089,
/// ADR-0540): curl 8.22.0 built from the release tarball with OpenSSL and no QUIC library, the Linux and macOS
/// reference, refuses both with exit 2 and
/// <c>curl: option --http3: the installed libcurl version does not support this</c>, as the Windows Schannel
/// build does (ADR-0397), so Curl refuses them on every platform unless the parse reads as a QUIC build
/// (<see cref="CommandLineOptions.ActsAsQuicBuild"/>), which only tests and the conformance harness ask for.
/// </summary>
[TestClass]
public sealed class CommandLineHttp3RefusalTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string NotSupported = "the installed libcurl version does not support this";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--http3", false)]
    [DataRow("--http3-only", false)]
    [DataRow("--http3=x", false)]
    [DataRow("--http3", true)]
    [DataRow("--http3-only", true)]
    public void Parse_Http3OptionOnAReferenceBuild_IsRefusedAsNotSupported(string spelling, bool isWindows)
    {
        Diagnostics.Arrange("is windows", isWindows);

        CommandLineParseResult result = Parse([spelling, Url], parsed => CommandLineParser.Parse(parsed, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows));

        AssertRefused(result, $"curl: option {spelling}: {NotSupported}");
    }

    [TestMethod]
    public void Parse_Http3OptionAfterAnotherVersionOnTheOpenSslBuild_IsRefusedAtTheHttp3Option()
    {
        CommandLineParseResult result = Parse(["--http1.1", "--http3-only", Url], parsed => CommandLineParser.Parse(parsed, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: false));

        AssertRefused(result, $"curl: option --http3-only: {NotSupported}");
    }

    [TestMethod]
    [DataRow("--no-http3")]
    [DataRow("--no-http3-only")]
    public void Parse_NegatedHttp3OptionOnTheOpenSslBuild_IsRefusedAsNotReversible(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, Url], parsed => CommandLineParser.Parse(parsed, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: false));

        AssertRefused(result, $"curl: option {spelling}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_DefaultConfigFileSearchOverloadOffWindows_RefusesHttp3()
    {
        CommandLineParseResult result = Parse(
            ["-q", "--http3", Url],
            parsed => CommandLineParser.Parse(parsed, _ => false, new EmptyPasswordPrompt(), new RecordingDataFileReader(), new DefaultConfigFileSearch(_ => null, isWindows: false, null, null), isWindows: false));

        AssertRefused(result, $"curl: option --http3: {NotSupported}");
    }

    [TestMethod]
    [DataRow("--http3", RequestedHttpVersion.Http3)]
    [DataRow("--http3-only", RequestedHttpVersion.Http3Only)]
    public void Parse_Http3OptionOnAQuicBuild_IsAcceptedAndSelectsTheVersion(string spelling, RequestedHttpVersion expected)
    {
        CommandLineParseResult result = Parse([spelling, Url], QuicBuildParser.Parse);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ActsAsQuicBuild);
        Assert.AreEqual(expected, result.Options.HttpVersion);
    }

    [TestMethod]
    public void Parse_WithoutAskingForAQuicBuild_DoesNotActAsOne()
    {
        CommandLineParseResult result = Parse([Url], parsed => CommandLineParser.Parse(parsed, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: false));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ActsAsQuicBuild);
    }

    [TestMethod]
    [DataRow("## --http3\n")]
    [DataRow("## --http3-only\n")]
    public void AiHelp_SectionOfAnHttp3Option_SaysEveryPlatformRefusesIt(string heading)
    {
        string section = AiHelpSection(heading);

        Diagnostics.Assert("section says every platform refuses", true, section.Contains("- On every platform: refused with exit 2 (`the installed libcurl version does not support this`)", StringComparison.Ordinal));
        Diagnostics.Assert("section says windows refuses", false, section.Contains("On Windows: refused", StringComparison.Ordinal));
        StringAssert.Contains(section, "- On every platform: refused with exit 2 (`the installed libcurl version does not support this`)");
        Assert.DoesNotContain("On Windows: refused", section);
    }

    [TestMethod]
    [DataRow("## --http2\n")]
    [DataRow("## --http1.1\n")]
    public void AiHelp_SectionOfAnOptionSomeReferenceBuildHas_SaysNothingAboutEveryPlatform(string heading)
    {
        string section = AiHelpSection(heading);

        Diagnostics.Assert("section says every platform refuses", false, section.Contains("On every platform: refused", StringComparison.Ordinal));
        Assert.DoesNotContain("On every platform: refused", section);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<IReadOnlyList<string>, CommandLineParseResult> parse)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("acts as quic build", result.Options.ActsAsQuicBuild);
            Diagnostics.Act("HTTP version", result.Options.HttpVersion);
        }

        return result;
    }

    private string AiHelpSection(string heading)
    {
        Diagnostics.Arrange("ai help category", "all");
        Diagnostics.Arrange("heading", heading.ReplaceLineEndings("\\n"));
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("all", out string markdown));
        int start = markdown.IndexOf(heading, StringComparison.Ordinal);
        Diagnostics.Act("heading index", start);
        Assert.IsGreaterThanOrEqualTo(0, start);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];
        Diagnostics.Act("section", section.ReplaceLineEndings("\\n"));
        return section;
    }

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [optionLine, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }
}
