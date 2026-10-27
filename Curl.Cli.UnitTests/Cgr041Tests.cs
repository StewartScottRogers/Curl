using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--dns-servers</c>, <c>--dns-interface</c>, <c>--dns-ipv4-addr</c> and <c>--dns-ipv6-addr</c> as every
/// reference build answers them (third-party gap report issue CGR-041, ADR-0837): curl's Schannel build, measured
/// with 8.21.0 on 2026-10-26 (BL-3570), and curl 8.22.0 Schannel and 8.18.0 OpenSSL, as the report measured them,
/// have no c-ares and refuse each with exit 2 and <c>curl: option --dns-servers: the installed libcurl version does
/// not support this</c>, whatever its value, so Curl refuses them on every platform unless the parse reads as a
/// c-ares build (<see cref="CommandLineOptions.ActsAsCAresBuild"/>), which only tests ask for.
/// </summary>
[TestClass]
public sealed class Cgr041Tests
{
    private const string Url = "http://127.0.0.1:20000/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--dns-servers", new[] { "--dns-servers", "127.0.0.1", "-s", Url }, false)]
    [DataRow("--dns-servers", new[] { "--dns-servers", "127.0.0.1", "-s", Url }, true)]
    [DataRow("--dns-interface", new[] { "--dns-interface", "lo", "-s", Url }, false)]
    [DataRow("--dns-interface", new[] { "--dns-interface", "lo", "-s", Url }, true)]
    [DataRow("--dns-ipv4-addr", new[] { "-sS", "--dns-ipv4-addr", "127.0.0.1", "http://127.0.0.1:20600/x" }, false)]
    [DataRow("--dns-ipv4-addr", new[] { "-sS", "--dns-ipv4-addr", "127.0.0.1", "http://127.0.0.1:20600/x" }, true)]
    [DataRow("--dns-ipv6-addr", new[] { "-sS", "--dns-ipv6-addr", "::1", Url }, true)]
    [DataRow("--dns-servers", new[] { "--dns-servers", "", Url }, true)]
    [DataRow("--dns-servers", new[] { "--dns-servers", Url }, true)]
    public void Parse_DnsOptionOnAReferenceBuild_IsRefusedAsNotSupported(string option, string[] arguments, bool isWindows)
    {
        Diagnostics.Arrange("is windows", isWindows);
        Diagnostics.ArrangeArguments(arguments);
        string refusal = $"curl: option {option}: the installed libcurl version does not support this";

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows);

        Diagnostics.ActParse(result);
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [refusal, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { refusal, CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_DnsServersNegated_IsStillRefusedAsNotReversible()
    {
        string[] arguments = ["--no-dns-servers", "x", Url];
        Diagnostics.ArrangeArguments(arguments);
        string refusal = "curl: option --no-dns-servers: the given option cannot be reversed with a --no- prefix";

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: true);

        Diagnostics.ActParse(result);
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [refusal, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(refusal, result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_DnsServersOnACAresBuild_IsAcceptedWithItsList()
    {
        string[] arguments = ["--dns-servers", "127.0.0.1", Url];
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CAresBuildParser.Parse(arguments);

        Diagnostics.ActParse(result);
        Diagnostics.Assert("DNS servers", "127.0.0.1", result.IsAccepted ? result.Options.DnsServers : null);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ActsAsCAresBuild);
        Assert.AreEqual("127.0.0.1", result.Options.DnsServers);
    }

    [TestMethod]
    public void Parse_WithoutAskingForACAresBuild_DoesNotActAsOne()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: false);

        Diagnostics.ActParse(result);
        Diagnostics.Assert("acts as c-ares build", false, result.IsAccepted && result.Options.ActsAsCAresBuild);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ActsAsCAresBuild);
    }

    [TestMethod]
    [DataRow("dns-servers")]
    [DataRow("dns-interface")]
    [DataRow("dns-ipv4-addr")]
    [DataRow("dns-ipv6-addr")]
    public void AiHelp_DnsSection_SaysEveryPlatformRefusesIt(string longName)
    {
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("all", out string help));
        int start = help.IndexOf($"## --{longName}\n", StringComparison.Ordinal);
        int end = help.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = help[start..(end < 0 ? help.Length : end)];

        Diagnostics.Assert("section says every platform refuses", true, section.Contains("built without c-ares", StringComparison.Ordinal));
        StringAssert.Contains(section, "- On every platform: refused with exit 2 (`the installed libcurl version does not support this`), as curl's reference builds (Schannel on Windows, OpenSSL on Linux and macOS) are built without c-ares.");
    }
}
