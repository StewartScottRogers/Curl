using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_WithoutDohOptions_LeavesThemUnset()
    {
        CommandLineOptions options = Accept();

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
        Assert.AreEqual(url, Accept("--doh-url", url).DohUrl);
    }

    [TestMethod]
    public void Parse_DohUrlGivenTwice_KeepsTheLast()
    {
        Assert.AreEqual("https://b.example/", Accept("--doh-url", "https://a.example/", "--doh-url", "https://b.example/").DohUrl);
    }

    [TestMethod]
    public void Parse_EmptyDohUrlAfterOne_TurnsDohOff()
    {
        Assert.IsNull(Accept("--doh-url", "https://a.example/", "--doh-url", "").DohUrl);
    }

    [TestMethod]
    public void Parse_DohUrlLast_IsRefusedAsNeedingParameter()
    {
        AssertRefused(CommandLineParser.Parse([Url, "--doh-url"]), "curl: option --doh-url: requires parameter");
    }

    [TestMethod]
    public void Parse_NegatedDohUrl_IsRefusedAsNotReversible()
    {
        AssertRefused(
            CommandLineParser.Parse(["--no-doh-url", "x", Url]),
            "curl: option --no-doh-url: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_DohInsecure_SetsTheFlagAndItsNegationClearsIt()
    {
        Assert.IsTrue(Accept("--doh-insecure").DohInsecure);
        Assert.IsFalse(Accept("--doh-insecure", "--no-doh-insecure").DohInsecure);
    }

    [TestMethod]
    public void Parse_DohCertStatus_SetsTheFlagAndItsNegationClearsIt()
    {
        Assert.IsTrue(Accept("--doh-cert-status").DohCertificateStatus);
        Assert.IsFalse(Accept("--doh-cert-status", "--no-doh-cert-status").DohCertificateStatus);
    }

    [TestMethod]
    public void Parse_DohFlags_DoNotSetTheTransfersOwnTlsFlags()
    {
        CommandLineOptions options = Accept("--doh-insecure", "--doh-cert-status");

        Assert.IsFalse(options.Insecure);
        Assert.IsFalse(options.RequireCertificateStatus);
    }

    [TestMethod]
    public void Parse_DohUrlBeforeNext_DoesNotReachTheNextGroup()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--doh-url", "https://a.example/", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("https://a.example/", result.Groups[0].DohUrl);
        Assert.IsNull(result.Groups[1].DohUrl);
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
