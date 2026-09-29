using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the GSS-API options <c>--delegation</c>, <c>--service-name</c> and
/// <c>--proxy-service-name</c>, and that <c>--krb</c> is one of curl's no-function options (ADR-0142).
/// Measured with <c>Record-CurlExchange.ps1 -Ftp</c> against the local curl 8.21.0 (Schannel, Windows) and
/// with curl 8.18.0 (OpenSSL, mit-krb5, Ubuntu under WSL) on 2026-09-28; both answer alike (BL-630 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineGssApiOptionTests
{
    private const string Url = "ftp://127.0.0.1:1/x";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_NoGssApiOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(GssApiDelegation.None, result.Options.GssApiDelegation);
        Assert.IsNull(result.Options.ServiceName);
        Assert.IsNull(result.Options.ProxyServiceName);
    }

    [TestMethod]
    [DataRow("none", GssApiDelegation.None)]
    [DataRow("policy", GssApiDelegation.Policy)]
    [DataRow("always", GssApiDelegation.Always)]
    [DataRow("NONE", GssApiDelegation.None)]
    [DataRow("Policy", GssApiDelegation.Policy)]
    [DataRow("ALWAYS", GssApiDelegation.Always)]
    public void Parse_DelegationCurlRecognises_SetsItWithoutWarning(string value, GssApiDelegation expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--delegation", "always", "--delegation", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.GssApiDelegation);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    public void Parse_DelegationCurlDoesNotRecognise_WarnsAndUsesNone(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--delegation", "always", "--delegation", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(GssApiDelegation.None, result.Options.GssApiDelegation);
        CollectionAssert.AreEqual(
            new[] { $"Warning: unrecognized delegation method '{value}', using none" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_DelegationLongEnoughToWrap_WarnsOnTwoLinesAsCurlDoes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--delegation", "aaaa bbbb cccc dddd eeee ffff gggg hhhh iiii jjjj kkkk llll mmmm", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: unrecognized delegation method 'aaaa bbbb cccc dddd eeee ffff gggg ",
                "Warning: hhhh iiii jjjj kkkk llll mmmm', using none",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentBeforeUnrecognisedDelegation_DropsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--delegation", "bogus", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SilentAfterUnrecognisedDelegation_KeepsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--delegation", "bogus", "-s", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: unrecognized delegation method 'bogus', using none" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_ServiceName_RecordsTheLastValueVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--service-name", "first", "--service-name", "HTTP/host@REALM", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("HTTP/host@REALM", result.Options.ServiceName);
        Assert.IsNull(result.Options.ProxyServiceName);
    }

    [TestMethod]
    public void Parse_ProxyServiceName_RecordsTheLastValueVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-service-name", "first", "--proxy-service-name", "proxy", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("proxy", result.Options.ProxyServiceName);
        Assert.IsNull(result.Options.ServiceName);
    }

    [TestMethod]
    [DataRow("--service-name")]
    [DataRow("--proxy-service-name")]
    public void Parse_EmptyServiceName_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, string.Empty, Url]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: blank argument where content is expected", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-delegation")]
    [DataRow("--no-service-name")]
    [DataRow("--no-proxy-service-name")]
    [DataRow("--no-krb")]
    public void Parse_NegatedGssApiOption_IsRefusedAsNotReversible(string argument)
    {
        CommandLineParseResult result = CommandLineParser.Parse([argument, "x", Url]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("private")]
    [DataRow("bogus")]
    [DataRow("CLEAR")]
    [DataRow("")]
    public void Parse_Krb_TakesAnyLevelAndWarnsThatItHasNoFunction(string level)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--krb", level, Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Warning: --krb is deprecated and has no function anymore" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_KrbLast_RequiresParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--krb"]);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --krb: requires parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
