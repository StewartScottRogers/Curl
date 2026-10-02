using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--proxy-tlsuser</c>, <c>--proxy-tlspassword</c> and
/// <c>--proxy-tlsauthtype</c> (BL-1135), measured with curl 8.18.0's OpenSSL build on 2026-10-02: an empty
/// <c>--proxy-tlsuser</c> is accepted while an empty <c>--proxy-tlspassword</c> is refused as blank, the
/// opposite of <c>--tlsuser</c> and <c>--tlspassword</c>; <c>--proxy-tlsauthtype</c> refuses an empty value as
/// blank and anything but <c>SRP</c> as unsupported; and none of the three is reversible with <c>--no-</c>.
/// </summary>
[TestClass]
public sealed class CommandLineProxyTlsSrpOptionTests
{
    private const string Url = "https://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesThemNotGiven()
    {
        CommandLineOptions options = CommandLineParser.Parse([Url]).Options!;

        Assert.IsNull(options.ProxyTlsUser);
        Assert.IsNull(options.ProxyTlsPassword);
        Assert.IsNull(options.ProxyTlsAuthType);
    }

    [TestMethod]
    public void Parse_EveryOption_RecordsTheLastValueVerbatimApartFromTheTargetOptions()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
        [
            "--proxy-tlsuser", "u1", "--proxy-tlsuser", "proxyuser",
            "--proxy-tlspassword", "p1", "--proxy-tlspassword", "proxysecret",
            "--proxy-tlsauthtype", "SRP",
            Url,
        ]);

        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions options = result.Options;
        Assert.AreEqual("proxyuser", options.ProxyTlsUser);
        Assert.AreEqual("proxysecret", options.ProxyTlsPassword);
        Assert.AreEqual("SRP", options.ProxyTlsAuthType);
        Assert.IsNull(options.TlsUser);
        Assert.IsNull(options.TlsPassword);
        Assert.IsNull(options.TlsAuthType);
    }

    [TestMethod]
    public void Parse_EmptyProxyTlsUser_IsAccepted()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-tlsuser", string.Empty, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.ProxyTlsUser);
    }

    [TestMethod]
    [DataRow("--proxy-tlspassword")]
    [DataRow("--proxy-tlsauthtype")]
    public void Parse_EmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, string.Empty, Url]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: blank argument where content is expected", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("srp")]
    [DataRow("SRP ")]
    public void Parse_ProxyTlsAuthTypeOtherThanSrp_RefusesAsUnsupported(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-tlsauthtype", value, Url]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --proxy-tlsauthtype: the installed libcurl version does not support this", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-proxy-tlsuser")]
    [DataRow("--no-proxy-tlspassword")]
    [DataRow("--no-proxy-tlsauthtype")]
    public void Parse_NegatedOption_CannotBeReversed(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

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
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("proxy", out string markdown));
        int start = markdown.IndexOf(heading, StringComparison.Ordinal);
        int end = markdown.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? markdown[start..] : markdown[start..end];

        Assert.IsGreaterThanOrEqualTo(0, start);
        Assert.DoesNotContain("Not supported by this build yet", section);
    }
}
