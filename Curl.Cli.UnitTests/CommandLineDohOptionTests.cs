using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--doh-url</c>, <c>--doh-insecure</c> and <c>--doh-cert-status</c> as curl 8.21.0 (Schannel)
/// parses them, measured with <c>Record-CurlExchange.ps1</c> on 2026-09-29 (BL-642 Notes): the last URL is
/// kept verbatim and unchecked, an empty one is accepted and turns DoH off, a missing one is refused as
/// needing a parameter, <c>--no-doh-url</c> cannot be reversed, and the two flags take <c>--no-</c>.
/// </summary>
[TestClass]
public sealed class CommandLineDohOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_WithoutDohOptions_LeavesThemUnset()
    {
        CommandLineOptions options = Accept();

        Diagnostics.Assert("doh url", null, options.DohUrl);
        Diagnostics.Assert("doh insecure", false, options.DohInsecure);
        Diagnostics.Assert("doh cert status", false, options.DohCertificateStatus);
        Assert.IsNull(options.DohUrl);
        Assert.IsFalse(options.DohInsecure);
        Assert.IsFalse(options.DohCertificateStatus);
    }

    [TestMethod]
    [DataRow("https://dns.example/dns-query")]
    [DataRow("bogus")]
    [DataRow("ftp://127.0.0.1/")]
    public void Parse_DohUrl_RecordsTheValueVerbatim(string url)
    {
        string? dohUrl = Accept("--doh-url", url).DohUrl;

        Diagnostics.Assert("doh url", url, dohUrl);
        Assert.AreEqual(url, dohUrl);
    }

    [TestMethod]
    public void Parse_DohUrlGivenTwice_KeepsTheLast()
    {
        string? dohUrl = Accept("--doh-url", "https://a.example/", "--doh-url", "https://b.example/").DohUrl;

        Diagnostics.Assert("doh url", "https://b.example/", dohUrl);
        Assert.AreEqual("https://b.example/", dohUrl);
    }

    [TestMethod]
    public void Parse_EmptyDohUrlAfterOne_TurnsDohOff()
    {
        string? dohUrl = Accept("--doh-url", "https://a.example/", "--doh-url", "").DohUrl;

        Diagnostics.Assert("doh url", null, dohUrl);
        Assert.IsNull(dohUrl);
    }

    [TestMethod]
    public void Parse_DohUrlLast_IsRefusedAsNeedingParameter()
    {
        AssertRefused(Parse([Url, "--doh-url"]), "curl: option --doh-url: requires parameter");
    }

    [TestMethod]
    public void Parse_NegatedDohUrl_IsRefusedAsNotReversible()
    {
        AssertRefused(
            Parse(["--no-doh-url", "x", Url]),
            "curl: option --no-doh-url: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_DohInsecure_SetsTheFlagAndItsNegationClearsIt()
    {
        bool set = Accept("--doh-insecure").DohInsecure;
        bool cleared = Accept("--doh-insecure", "--no-doh-insecure").DohInsecure;

        Diagnostics.Assert("doh insecure after --doh-insecure", true, set);
        Diagnostics.Assert("doh insecure after --no-doh-insecure", false, cleared);
        Assert.IsTrue(set);
        Assert.IsFalse(cleared);
    }

    [TestMethod]
    public void Parse_DohCertStatus_SetsTheFlagAndItsNegationClearsIt()
    {
        bool set = Accept("--doh-cert-status").DohCertificateStatus;
        bool cleared = Accept("--doh-cert-status", "--no-doh-cert-status").DohCertificateStatus;

        Diagnostics.Assert("doh cert status after --doh-cert-status", true, set);
        Diagnostics.Assert("doh cert status after --no-doh-cert-status", false, cleared);
        Assert.IsTrue(set);
        Assert.IsFalse(cleared);
    }

    [TestMethod]
    public void Parse_DohFlags_DoNotSetTheTransfersOwnTlsFlags()
    {
        CommandLineOptions options = Accept("--doh-insecure", "--doh-cert-status");

        Diagnostics.Assert("insecure", false, options.Insecure);
        Diagnostics.Assert("require certificate status", false, options.RequireCertificateStatus);
        Assert.IsFalse(options.Insecure);
        Assert.IsFalse(options.RequireCertificateStatus);
    }

    [TestMethod]
    public void Parse_DohUrlBeforeNext_DoesNotReachTheNextGroup()
    {
        CommandLineParseResult result = Parse(["--doh-url", "https://a.example/", Url, "--next", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("group 0 doh url", "https://a.example/", result.Groups[0].DohUrl);
        Diagnostics.Assert("group 1 doh url", null, result.Groups[1].DohUrl);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("https://a.example/", result.Groups[0].DohUrl);
        Assert.IsNull(result.Groups[1].DohUrl);
    }

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach([optionLine, TryHelp]), CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
