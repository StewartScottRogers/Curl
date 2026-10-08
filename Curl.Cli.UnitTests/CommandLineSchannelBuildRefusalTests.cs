using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the options curl's Windows Schannel build refuses because its libcurl was built without the
/// feature (ADR-0397, audit finding AF-0022): <c>--http2</c>, <c>--http2-prior-knowledge</c>, <c>--http3</c>,
/// <c>--http3-only</c>, <c>--tlsuser</c>, <c>--tlspassword</c>, <c>--tlsauthtype</c>, their <c>--proxy-</c> forms
/// and <c>--ssl-sessions</c>. Every line was measured with curl 8.21.0 (x86_64-w64-mingw32) Schannel against
/// <c>http://127.0.0.1:1/</c> on 2026-10-02 (BL-1278). Each test parses as the Windows build explicitly,
/// so it runs on every platform.
/// </summary>
[TestClass]
public sealed class CommandLineSchannelBuildRefusalTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string NotSupported = "the installed libcurl version does not support this";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<IReadOnlyList<string>, CommandLineParseResult> parse)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("acts as windows schannel build", result.Options.ActsAsWindowsSchannelBuild);
            Diagnostics.Act("proxy http2", result.Options.ProxyHttp2);
        }

        return result;
    }

    [TestMethod]
    [DataRow("--http2")]
    [DataRow("--http2-prior-knowledge")]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    [DataRow("--http2=x")]
    public void Parse_HttpVersionTheSchannelBuildLacks_IsRefusedAsNotSupported(string spelling)
    {
        AssertRefused(ParseAsWindowsBuild(["-s", spelling, Url]), $"curl: option {spelling}: {NotSupported}");
    }

    [TestMethod]
    [DataRow("--tlsuser", "1")]
    [DataRow("--tlspassword", "p")]
    [DataRow("--tlsauthtype", "SRP")]
    [DataRow("--proxy-tlsuser", "u")]
    [DataRow("--proxy-tlspassword", "p")]
    [DataRow("--proxy-tlsauthtype", "SRP")]
    [DataRow("--ssl-sessions", "f.txt")]
    public void Parse_ValueOptionTheSchannelBuildLacks_IsRefusedAsNotSupported(string spelledOption, string value)
    {
        AssertRefused(ParseAsWindowsBuild(["-s", spelledOption, value, Url]), $"curl: option {spelledOption}: {NotSupported}");
    }

    [TestMethod]
    [DataRow("--tlsuser")]
    [DataRow("--tlspassword")]
    [DataRow("--tlsauthtype")]
    [DataRow("--ssl-sessions")]
    public void Parse_BlankValueOfAnOptionTheSchannelBuildLacks_IsRefusedAsNotSupportedNotAsBlank(string spelledOption)
    {
        AssertRefused(ParseAsWindowsBuild([spelledOption, string.Empty, Url]), $"curl: option {spelledOption}: {NotSupported}");
    }

    [TestMethod]
    [DataRow("--tlsuser")]
    [DataRow("--ssl-sessions")]
    public void Parse_OptionTheSchannelBuildLacksAsTheLastArgument_IsRefusedAsRequiringAParameter(string spelledOption)
    {
        AssertRefused(ParseAsWindowsBuild(["-s", spelledOption]), $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    public void Parse_Http2BeforeAnotherVersion_IsRefusedAtTheHttp2Option()
    {
        AssertRefused(ParseAsWindowsBuild(["--http2", "--http1.1", Url]), $"curl: option --http2: {NotSupported}");
    }

    [TestMethod]
    public void Parse_NegatedHttp2_IsStillRefusedAsNotReversible()
    {
        AssertRefused(
            ParseAsWindowsBuild(["--no-http2", Url]),
            "curl: option --no-http2: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_ConfigFileLineNamingAnOptionTheSchannelBuildLacks_IsRefusedAsNotSupported()
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.txt"] = "tlsuser = x\n"u8.ToArray();
        Diagnostics.Bytes("k.txt", reader.Files["k.txt"]);

        CommandLineParseResult result = Parse(["-K", "k.txt", Url], parsed => CommandLineParser.Parse(parsed, _ => true, ConsolePasswordPrompt.ForProcessConsole, reader, isWindows: true));

        Diagnostics.AssertRefusal(
            result,
            CurlExitCode.FailedInit,
            [
                "curl: k.txt:1 config file option 'tlsuser' the installed libcurl version does ",
                "curl: not support this",
                $"curl: option -K: {NotSupported}",
                CommandLineRefusal.TryHelpLine,
            ]);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: k.txt:1 config file option 'tlsuser' the installed libcurl version does ",
                "curl: not support this",
                $"curl: option -K: {NotSupported}",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--http2")]
    [DataRow("--http3-only")]
    public void Parse_HttpVersionOnTheOpenSslBuild_IsAccepted(string spelling)
    {
        CommandLineParseResult result = ParseAsOpenSslBuild(["-s", spelling, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ActsAsWindowsSchannelBuild);
    }

    [TestMethod]
    public void Parse_ProxyHttp2OnTheOpenSslBuild_SetsProxyHttp2()
    {
        CommandLineParseResult result = ParseAsOpenSslBuild(["--proxy-http2", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ProxyHttp2);
    }

    [TestMethod]
    public void Parse_NoProxyHttp2AfterProxyHttp2OnTheOpenSslBuild_ClearsProxyHttp2()
    {
        CommandLineParseResult result = ParseAsOpenSslBuild(["--proxy-http2", "--no-proxy-http2", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ProxyHttp2);
    }

    [TestMethod]
    public void Parse_NoProxyOption_LeavesProxyHttp2Off()
    {
        CommandLineParseResult result = ParseAsOpenSslBuild([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ProxyHttp2);
    }

    [TestMethod]
    [DataRow("--proxy-http2")]
    [DataRow("--no-proxy-http2")]
    public void Parse_ProxyHttp2OnTheSchannelBuild_IsRefusedAsNotSupported(string spelling)
    {
        AssertRefused(ParseAsWindowsBuild(["-s", spelling, Url]), $"curl: option {spelling}: {NotSupported}");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Parse_ProxyHttp3OnEitherBuild_IsRefusedAsNotSupported(bool isWindows)
    {
        Diagnostics.Arrange("is windows", isWindows);
        CommandLineParseResult result = Parse(["-s", "--proxy-http3", Url], parsed => CommandLineParser.Parse(parsed, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), isWindows));

        AssertRefused(result, $"curl: option --proxy-http3: {NotSupported}");
    }

    [TestMethod]
    public void AiHelp_ProxyHttp3Section_SaysItIsRefusedEverywhere()
    {
        string section = AiHelpSection("## --proxy-http3\n");

        Diagnostics.Assert("section says not supported", true, section.Contains("- Not supported by this build yet: curl refuses it with exit 2.", StringComparison.Ordinal));
        Diagnostics.Assert("section says windows refuses", false, section.Contains("On Windows: refused", StringComparison.Ordinal));
        StringAssert.Contains(section, "- Not supported by this build yet: curl refuses it with exit 2.");
        Assert.DoesNotContain("On Windows: refused", section);
    }

    [TestMethod]
    public void Parse_DefaultConfigFileSearchOverloadAsTheWindowsBuild_RefusesHttp3()
    {
        CommandLineParseResult result = Parse(
            ["-q", "--http3", Url],
            parsed => CommandLineParser.Parse(
                parsed,
                _ => false,
                ConsolePasswordPrompt.ForProcessConsole,
                new RecordingDataFileReader(),
                new DefaultConfigFileSearch(_ => null, isWindows: true, null, null),
                isWindows: true));

        AssertRefused(result, $"curl: option --http3: {NotSupported}");
    }

    [TestMethod]
    [DataRow("## --http2\n")]
    [DataRow("## --http3-only\n")]
    [DataRow("## --tlsuser\n")]
    [DataRow("## --proxy-tlsauthtype\n")]
    [DataRow("## --ssl-sessions\n")]
    [DataRow("## --proxy-http2\n")]
    public void AiHelp_SectionOfAnOptionTheSchannelBuildLacks_SaysWindowsRefusesIt(string heading)
    {
        string section = AiHelpSection(heading);

        Diagnostics.Assert("section says windows refuses", true, section.Contains("- On Windows: refused with exit 2 (`the installed libcurl version does not support this`)", StringComparison.Ordinal));
        StringAssert.Contains(section, "- On Windows: refused with exit 2 (`the installed libcurl version does not support this`)");
    }

    [TestMethod]
    [DataRow("## --http1.1\n")]
    [DataRow("## --tlsv1.2\n")]
    public void AiHelp_SectionOfAnOptionTheSchannelBuildHas_SaysNothingAboutWindows(string heading)
    {
        string section = AiHelpSection(heading);

        Diagnostics.Assert("section says windows refuses", false, section.Contains("On Windows: refused", StringComparison.Ordinal));
        Assert.DoesNotContain("On Windows: refused", section);
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

    private CommandLineParseResult ParseAsWindowsBuild(IReadOnlyList<string> arguments) =>
        Parse(arguments, parsed => CommandLineParser.Parse(parsed, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), isWindows: true));

    private CommandLineParseResult ParseAsOpenSslBuild(IReadOnlyList<string> arguments) =>
        Parse(arguments, parsed => CommandLineParser.Parse(parsed, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), isWindows: false));

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [optionLine, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }
}
