using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--proxy-tlsuser</c>, <c>--proxy-tlspassword</c> and
/// <c>--proxy-tlsauthtype</c>: since curl 8.22.0 each only warns that it has no function (ADR-0480, pinned by
/// <see cref="CommandLineNoFunctionOptionTests"/>), so none records anything, none is reversible with
/// <c>--no-</c>, and their <c>--ai-help</c> sections do not call them unsupported.
/// </summary>
[TestClass]
public sealed class CommandLineProxyTlsSrpOptionTests
{
    private const string Url = "https://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--no-proxy-tlsuser")]
    [DataRow("--no-proxy-tlspassword")]
    [DataRow("--no-proxy-tlsauthtype")]
    public void Parse_NegatedOption_CannotBeReversed(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        AssertRefusal([$"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix", TryHelp], result);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("## --proxy-tlsuser\n")]
    [DataRow("## --proxy-tlspassword\n")]
    [DataRow("## --proxy-tlsauthtype\n")]
    public void AiHelp_ProxyTlsSrpSection_DoesNotSayItIsNotSupported(string heading)
    {
        Diagnostics.Arrange("category", "proxy");
        Diagnostics.Arrange("heading", heading.TrimEnd('\n'));

        bool found = CurlAiHelpText.TryGetMarkdown("proxy", out string markdown);
        Diagnostics.Act("category found", found);
        Assert.IsTrue(found);
        int start = markdown.IndexOf(heading, StringComparison.Ordinal);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];
        Diagnostics.Act("section start", start);
        Diagnostics.Act("section length", section.Length);

        Diagnostics.Assert(
            "section says not supported",
            false,
            section.Contains("Not supported by this build yet", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.DoesNotContain("Not supported by this build yet", section);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("build", "OpenSSL");
        CommandLineParseResult result = OpenSslBuildParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertText(string label, string? expected, string? actual) =>
        Diagnostics.Assert(label, Quote(expected), Quote(actual));

    private static string Quote(string? value) => value is null ? "null" : "\"" + value + "\"";

    private void AssertRefusal(string[] expectedLines, CommandLineParseResult result)
    {
        CommandLineRefusal? refusal = CommandLineParseDiagnostics.Peek(result.Refusal);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
    }
}
