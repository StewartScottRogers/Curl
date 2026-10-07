using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--ftp-account</c>, <c>--ftp-alternative-to-user</c>, <c>--ftp-pret</c>,
/// <c>--ftp-ssl-ccc</c> and <c>--ftp-ssl-ccc-mode</c>. Measured with <c>Record-CurlExchange.ps1 -Ftp</c>
/// against the local curl 8.21.0 (Schannel, Windows) on 2026-09-28 (BL-634 Notes): a bad
/// <c>--ftp-ssl-ccc-mode</c> is warned about and read as <c>passive</c>, not refused; the two text options
/// refuse an empty value as blank; only the two flags can be reversed with <c>--no-</c>.
/// </summary>
[TestClass]
public sealed class CommandLineFtpAccountPretAndCccOptionTests
{
    private const string Url = "ftp://127.0.0.1:1/x";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp account", null, result.Options?.FtpAccount);
        Diagnostics.Assert("ftp alternative to user", null, result.Options?.FtpAlternativeToUser);
        Diagnostics.Assert("ftp send pret", false, result.Options?.FtpSendPret);
        Diagnostics.Assert("ftp clear command channel", FtpClearCommandChannel.Off, result.Options?.FtpClearCommandChannel);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.FtpAccount);
        Assert.IsNull(result.Options.FtpAlternativeToUser);
        Assert.IsFalse(result.Options.FtpSendPret);
        Assert.AreEqual(FtpClearCommandChannel.Off, result.Options.FtpClearCommandChannel);
    }

    [TestMethod]
    public void Parse_FtpAccount_RecordsTheLastValueVerbatim()
    {
        CommandLineParseResult result = Parse(["--ftp-account", "first", "--ftp-account", "my account", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp account", "my account", result.Options?.FtpAccount);
        Diagnostics.Assert("ftp alternative to user", null, result.Options?.FtpAlternativeToUser);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("my account", result.Options.FtpAccount);
        Assert.IsNull(result.Options.FtpAlternativeToUser);
    }

    [TestMethod]
    public void Parse_FtpAlternativeToUser_RecordsTheLastValueVerbatim()
    {
        CommandLineParseResult result = Parse(
            ["--ftp-alternative-to-user", "first", "--ftp-alternative-to-user", "SITE AUTH x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp alternative to user", "SITE AUTH x", result.Options?.FtpAlternativeToUser);
        Diagnostics.Assert("ftp account", null, result.Options?.FtpAccount);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("SITE AUTH x", result.Options.FtpAlternativeToUser);
        Assert.IsNull(result.Options.FtpAccount);
    }

    [TestMethod]
    [DataRow("--ftp-account")]
    [DataRow("--ftp-alternative-to-user")]
    public void Parse_EmptyTextOption_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, string.Empty, Url]);

        string[] expectedLines = [$"curl: option {spelledOption}: blank argument where content is expected", TryHelp];
        AssertRefusalDiagnostics(result, expectedLines);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: blank argument where content is expected", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--ftp-account")]
    [DataRow("--ftp-alternative-to-user")]
    public void Parse_TextOptionBeforeTheUrl_TakesTheUrlAsItsValueAndFindsNoUrl(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        string[] expectedLines = ["curl: (2) no URL specified", TryHelp];
        AssertRefusalDiagnostics(result, expectedLines);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: (2) no URL specified", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--ftp-pret" }, true)]
    [DataRow(new[] { "--no-ftp-pret" }, false)]
    [DataRow(new[] { "--ftp-pret", "--no-ftp-pret" }, false)]
    [DataRow(new[] { "--no-ftp-pret", "--ftp-pret" }, true)]
    public void Parse_FtpPretAndItsNegation_TheLaterWins(string[] flags, bool expected)
    {
        CommandLineParseResult result = Parse([.. flags, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp send pret", expected, result.Options?.FtpSendPret);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpSendPret);
    }

    [TestMethod]
    [DataRow(new[] { "--ftp-ssl-ccc" }, FtpClearCommandChannel.Passive)]
    [DataRow(new[] { "--no-ftp-ssl-ccc" }, FtpClearCommandChannel.Off)]
    [DataRow(new[] { "--ftp-ssl-ccc", "--no-ftp-ssl-ccc" }, FtpClearCommandChannel.Off)]
    [DataRow(new[] { "--no-ftp-ssl-ccc", "--ftp-ssl-ccc" }, FtpClearCommandChannel.Passive)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "active" }, FtpClearCommandChannel.Active)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "active", "--ftp-ssl-ccc" }, FtpClearCommandChannel.Active)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "active", "--no-ftp-ssl-ccc" }, FtpClearCommandChannel.Off)]
    [DataRow(new[] { "--ftp-ssl-ccc-mode", "active", "--no-ftp-ssl-ccc", "--ftp-ssl-ccc" }, FtpClearCommandChannel.Active)]
    [DataRow(new[] { "--no-ftp-ssl-ccc", "--ftp-ssl-ccc-mode", "passive" }, FtpClearCommandChannel.Passive)]
    public void Parse_FtpSslCccAndItsMode_CombineAsCurlKeepsThem(string[] arguments, FtpClearCommandChannel expected)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp clear command channel", expected, result.Options?.FtpClearCommandChannel);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpClearCommandChannel);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("active", FtpClearCommandChannel.Active)]
    [DataRow("ACTIVE", FtpClearCommandChannel.Active)]
    [DataRow("passive", FtpClearCommandChannel.Passive)]
    [DataRow("Passive", FtpClearCommandChannel.Passive)]
    public void Parse_FtpSslCccModeCurlRecognises_SetsItWithoutWarning(string value, FtpClearCommandChannel expected)
    {
        CommandLineParseResult result = Parse(["--ftp-ssl-ccc-mode", value, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp clear command channel", expected, result.Options?.FtpClearCommandChannel);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpClearCommandChannel);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    public void Parse_FtpSslCccModeCurlDoesNotRecognise_WarnsAndUsesPassive(string value)
    {
        CommandLineParseResult result = Parse(["--ftp-ssl-ccc-mode", "active", "--ftp-ssl-ccc-mode", value, Url]);

        string[] expectedWarnings = [$"Warning: unrecognized ftp CCC method '{value}', using default"];
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp clear command channel", FtpClearCommandChannel.Passive, result.Options?.FtpClearCommandChannel);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expectedWarnings), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(FtpClearCommandChannel.Passive, result.Options.FtpClearCommandChannel);
        CollectionAssert.AreEqual(
            new[] { $"Warning: unrecognized ftp CCC method '{value}', using default" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FtpSslCccModeLongEnoughToWrap_WarnsOnThreeLinesAsCurlDoes()
    {
        CommandLineParseResult result = Parse(
            ["--ftp-ssl-ccc-mode", "a-very-long-bogus-value-that-should-make-the-warning-wrap-past-seventy-nine-columns", Url]);

        string[] expectedWarnings =
        [
            "Warning: unrecognized ftp CCC method ",
            "Warning: 'a-very-long-bogus-value-that-should-make-the-warning-wrap-past-sevent",
            "Warning: y-nine-columns', using default",
        ];
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expectedWarnings), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: unrecognized ftp CCC method ",
                "Warning: 'a-very-long-bogus-value-that-should-make-the-warning-wrap-past-sevent",
                "Warning: y-nine-columns', using default",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentBeforeUnrecognisedFtpSslCccMode_DropsTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "--ftp-ssl-ccc-mode", "bogus", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp clear command channel", FtpClearCommandChannel.Passive, result.Options?.FtpClearCommandChannel);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(FtpClearCommandChannel.Passive, result.Options.FtpClearCommandChannel);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--no-ftp-account")]
    [DataRow("--no-ftp-alternative-to-user")]
    [DataRow("--no-ftp-ssl-ccc-mode")]
    [DataRow("--no-ftp-ssl-ccc-mode=x")]
    public void Parse_NegatedTextOption_IsRefusedAsNotReversible(string argument)
    {
        CommandLineParseResult result = Parse([argument, "x", Url]);

        string[] expectedLines = [$"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp];
        AssertRefusalDiagnostics(result, expectedLines);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefusalDiagnostics(CommandLineParseResult result, string[] expectedLines)
    {
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
    }
}
