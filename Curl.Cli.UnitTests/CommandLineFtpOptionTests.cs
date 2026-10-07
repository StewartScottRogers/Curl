using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the FTP control options on <see cref="CommandLineOptions"/>:
/// <c>--disable-epsv</c>, <c>--ftp-skip-pasv-ip</c> (on by default), <c>--ftp-method</c>,
/// <c>--ftp-create-dirs</c>, <c>-l</c>/<c>--list-only</c> and every <c>-Q</c>/<c>--quote</c> in order.
/// Behaviour measured against the local curl 8.21.0 on 2026-09-27: a bad <c>--ftp-method</c> is
/// warned about and read as <c>multicwd</c>, not refused, and <c>-Q ''</c> is accepted.
/// </summary>
[TestClass]
public sealed class CommandLineFtpOptionTests
{
    private const string Url = "ftp://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoFtpOptions_LeavesThemAtCurlsDefaults()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp disable epsv", false, result.Options?.FtpDisableEpsv);
        Diagnostics.Assert("ftp skip pasv ip", true, result.Options?.FtpSkipPasvIp);
        Diagnostics.Assert("ftp file method", FtpFileMethod.MultiCwd, result.Options?.FtpFileMethod);
        Diagnostics.Assert("ftp create directories", false, result.Options?.FtpCreateDirectories);
        Diagnostics.Assert("list only", false, result.Options?.ListOnly);
        Diagnostics.Assert("quote commands", "[]", CommandLineParseDiagnostics.QuoteEach(result.Options?.QuoteCommands ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.FtpDisableEpsv);
        Assert.IsTrue(result.Options.FtpSkipPasvIp);
        Assert.AreEqual(FtpFileMethod.MultiCwd, result.Options.FtpFileMethod);
        Assert.IsFalse(result.Options.FtpCreateDirectories);
        Assert.IsFalse(result.Options.ListOnly);
        Assert.IsEmpty(result.Options.QuoteCommands);
    }

    [TestMethod]
    public void Parse_DisableEpsv_SetsFtpDisableEpsv()
    {
        CommandLineParseResult result = Parse(["--disable-epsv", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp disable epsv", true, result.Options?.FtpDisableEpsv);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.FtpDisableEpsv);
    }

    [TestMethod]
    [DataRow(new[] { "--disable-epsv", "--no-disable-epsv" }, false)]
    [DataRow(new[] { "--no-disable-epsv", "--disable-epsv" }, true)]
    public void Parse_DisableEpsvAndItsNegation_TheLaterWins(string[] flags, bool expected)
    {
        CommandLineParseResult result = Parse([.. flags, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp disable epsv", expected, result.Options?.FtpDisableEpsv);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpDisableEpsv);
    }

    [TestMethod]
    [DataRow(new[] { "--ftp-skip-pasv-ip" }, true)]
    [DataRow(new[] { "--no-ftp-skip-pasv-ip" }, false)]
    [DataRow(new[] { "--ftp-skip-pasv-ip", "--no-ftp-skip-pasv-ip" }, false)]
    [DataRow(new[] { "--no-ftp-skip-pasv-ip", "--ftp-skip-pasv-ip" }, true)]
    public void Parse_FtpSkipPasvIpAndItsNegation_TheLaterWins(string[] flags, bool expected)
    {
        CommandLineParseResult result = Parse([.. flags, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp skip pasv ip", expected, result.Options?.FtpSkipPasvIp);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpSkipPasvIp);
    }

    [TestMethod]
    [DataRow("multicwd", FtpFileMethod.MultiCwd)]
    [DataRow("MULTICWD", FtpFileMethod.MultiCwd)]
    [DataRow("nocwd", FtpFileMethod.NoCwd)]
    [DataRow("NoCwd", FtpFileMethod.NoCwd)]
    [DataRow("singlecwd", FtpFileMethod.SingleCwd)]
    [DataRow("SingleCwd", FtpFileMethod.SingleCwd)]
    public void Parse_FtpMethodCurlAccepts_SetsFtpFileMethodWithoutWarning(string value, FtpFileMethod expected)
    {
        CommandLineParseResult result = Parse(["--ftp-method", value, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp file method", expected, result.Options?.FtpFileMethod);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpFileMethod);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("single")]
    [DataRow("")]
    public void Parse_FtpMethodCurlDoesNotRecognise_WarnsAndUsesMultiCwd(string value)
    {
        CommandLineParseResult result = Parse(["--ftp-method", "nocwd", "--ftp-method", value, Url]);

        string[] expectedWarnings = [$"Warning: unrecognized ftp file method '{value}', using default"];
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp file method", FtpFileMethod.MultiCwd, result.Options?.FtpFileMethod);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expectedWarnings), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(FtpFileMethod.MultiCwd, result.Options.FtpFileMethod);
        CollectionAssert.AreEqual(
            new[] { $"Warning: unrecognized ftp file method '{value}', using default" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_FtpMethodLongEnoughToWrap_WarnsOnTwoLinesAsCurlDoes()
    {
        CommandLineParseResult result = Parse(
            ["--ftp-method", "aaaa bbbb cccc dddd eeee ffff gggg hhhh iiii jjjj kkkk llll mmmm", Url]);

        string[] expectedWarnings =
        [
            "Warning: unrecognized ftp file method 'aaaa bbbb cccc dddd eeee ffff gggg hhhh ",
            "Warning: iiii jjjj kkkk llll mmmm', using default",
        ];
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expectedWarnings), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: unrecognized ftp file method 'aaaa bbbb cccc dddd eeee ffff gggg hhhh ",
                "Warning: iiii jjjj kkkk llll mmmm', using default",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentThenBadFtpMethod_DropsTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "--ftp-method", "bogus", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp file method", FtpFileMethod.MultiCwd, result.Options?.FtpFileMethod);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(FtpFileMethod.MultiCwd, result.Options.FtpFileMethod);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(new[] { "--ftp-create-dirs" }, true)]
    [DataRow(new[] { "--ftp-create-dirs", "--no-ftp-create-dirs" }, false)]
    [DataRow(new[] { "--no-ftp-create-dirs", "--ftp-create-dirs" }, true)]
    public void Parse_FtpCreateDirsAndItsNegation_TheLaterWins(string[] flags, bool expected)
    {
        CommandLineParseResult result = Parse([.. flags, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("ftp create directories", expected, result.Options?.FtpCreateDirectories);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpCreateDirectories);
    }

    [TestMethod]
    [DataRow(new[] { "-l" }, true)]
    [DataRow(new[] { "--list-only" }, true)]
    [DataRow(new[] { "-l", "--no-list-only" }, false)]
    [DataRow(new[] { "--no-list-only", "-l" }, true)]
    public void Parse_ListOnlyAndItsNegation_TheLaterWins(string[] flags, bool expected)
    {
        CommandLineParseResult result = Parse([.. flags, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("list only", expected, result.Options?.ListOnly);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.ListOnly);
    }

    [TestMethod]
    public void Parse_RepeatedQuote_RecordsEveryCommandVerbatimInOrder()
    {
        CommandLineParseResult result = Parse(
            ["-Q", "NOOP", "--quote", "-DELE x", "-Q", "+*SITE CHMOD 644 y", "-Q", "", Url]);

        string[] expectedCommands = ["NOOP", "-DELE x", "+*SITE CHMOD 644 y", ""];
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("quote commands", CommandLineParseDiagnostics.QuoteEach(expectedCommands), CommandLineParseDiagnostics.QuoteEach(result.Options?.QuoteCommands ?? []));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "NOOP", "-DELE x", "+*SITE CHMOD 644 y", "" },
            result.Options.QuoteCommands.ToArray());
    }

    [TestMethod]
    [DataRow("--ftp-method")]
    [DataRow("-Q")]
    [DataRow("--quote")]
    public void Parse_ValueOptionAsLastArgument_RefusesAsRequiringParameter(string spelledOption)
    {
        CommandLineParseResult result = Parse([Url, spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    [DataRow("--no-ftp-method")]
    [DataRow("--no-quote")]
    public void Parse_NegatedValueOption_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "x", Url]);

        AssertRefused(result, $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix");
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([expectedFirstLine, CommandLineRefusal.TryHelpLine]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
