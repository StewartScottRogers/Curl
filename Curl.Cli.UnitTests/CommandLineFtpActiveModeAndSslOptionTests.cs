using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records FTP active mode and the TLS upgrade on <see cref="CommandLineOptions"/>
/// (ADR-0102): <c>-P</c>/<c>--ftp-port</c>, <c>--disable-eprt</c> and <c>--eprt</c>, <c>--ssl</c> and
/// <c>--ftp-ssl</c>, <c>--ssl-reqd</c> and <c>--ftp-ssl-reqd</c>, and <c>--ftp-ssl-control</c>.
/// Measured against the local curl 8.21.0 on 2026-09-27 with <c>Record-CurlExchange.ps1 -Ftp</c>, a
/// server that refuses <c>AUTH</c>, <c>EPRT</c> and <c>PORT</c>: <c>--ssl</c> sends <c>AUTH SSL</c> and
/// <c>AUTH TLS</c> and goes on in plaintext; <c>--ssl-reqd</c> and <c>--ftp-ssl-control</c> each fail
/// with exit 64; the three flags are independent, so <c>--ssl-reqd --no-ssl</c> still fails and
/// <c>--ssl --no-ssl-reqd</c> still tries; <c>--disable-eprt</c> and <c>--no-eprt</c> send <c>PORT</c>
/// only, and the later of <c>--disable-eprt</c>, <c>--eprt</c> and their negations wins.
/// <c>-P - --ftp-pasv</c> sends <c>EPSV</c> and <c>--ftp-pasv -P -</c> sends <c>EPRT</c> (the later wins);
/// <c>--no-ftp-pasv</c> exits 2 as not reversible; the later of <c>--disable-epsv</c>, <c>--epsv</c> and
/// their negations wins (<c>--no-epsv</c> sends <c>PASV</c>) - measured by BL-463 the same way.
/// </summary>
[TestClass]
public sealed class CommandLineFtpActiveModeAndSslOptionTests
{
    private const string Url = "ftp://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoActiveModeOrSslOptions_LeavesThemAtAdr0102Defaults()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.FtpPort);
        Assert.IsTrue(result.Options.FtpUseEprt);
        Assert.AreEqual(TransportSecurityLevel.None, result.Options.SslLevel);
        Assert.IsFalse(result.Options.FtpSslControlOnly);
    }

    [TestMethod]
    [DataRow("-P", "-")]
    [DataRow("-P", "192.168.0.10:32000-33000")]
    [DataRow("--ftp-port", "[::1]:4000")]
    public void Parse_FtpPort_RecordsTheAddressVerbatim(string option, string address)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, address, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(address, result.Options.FtpPort);
    }

    [TestMethod]
    public void Parse_FtpPortTwice_TheLaterWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-P", "-", "--ftp-port", "127.0.0.1", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("127.0.0.1", result.Options.FtpPort);
    }

    [TestMethod]
    [DataRow("-P")]
    [DataRow("--ftp-port")]
    public void Parse_FtpPortAsLastArgument_RequiresAParameterAsCurlDoes(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option]);

        AssertRefused(result, $"curl: option {option}: requires parameter");
    }

    [TestMethod]
    [DataRow("-P")]
    [DataRow("--ftp-port")]
    public void Parse_FtpPortBlank_IsRefusedAsBlankAsCurlDoes(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, string.Empty, Url]);

        AssertRefused(result, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_NegatedFtpPort_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-ftp-port", "-", Url]);

        AssertRefused(result, "curl: option --no-ftp-port: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("-P")]
    [DataRow("--ftp-port")]
    public void Parse_FtpPasvAfterFtpPort_ClearsTheAddressAsCurlDoes(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "-", "--ftp-pasv", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.FtpPort);
    }

    [TestMethod]
    public void Parse_FtpPortAfterFtpPasv_TheLaterWinsAsCurlDoes()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ftp-pasv", "-P", "-", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-", result.Options.FtpPort);
    }

    [TestMethod]
    public void Parse_FtpPasvAlone_LeavesPassiveMode()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ftp-pasv", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.FtpPort);
    }

    [TestMethod]
    public void Parse_NegatedFtpPasv_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-ftp-pasv", Url]);

        AssertRefused(result, "curl: option --no-ftp-pasv: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow(new[] { "--epsv" }, false)]
    [DataRow(new[] { "--no-epsv" }, true)]
    [DataRow(new[] { "--disable-epsv", "--epsv" }, false)]
    [DataRow(new[] { "--epsv", "--disable-epsv" }, true)]
    [DataRow(new[] { "--disable-epsv", "--no-epsv" }, true)]
    [DataRow(new[] { "--no-epsv", "--no-disable-epsv" }, false)]
    [DataRow(new[] { "--no-disable-epsv", "--no-epsv" }, true)]
    public void Parse_EpsvDisableEpsvAndTheirNegations_TheLaterWins(string[] flags, bool expectedDisabled)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. flags, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedDisabled, result.Options.FtpDisableEpsv);
    }

    [TestMethod]
    [DataRow(new[] { "--disable-eprt" }, false)]
    [DataRow(new[] { "--no-eprt" }, false)]
    [DataRow(new[] { "--eprt" }, true)]
    [DataRow(new[] { "--no-disable-eprt" }, true)]
    [DataRow(new[] { "--disable-eprt", "--no-disable-eprt" }, true)]
    [DataRow(new[] { "--no-eprt", "--eprt" }, true)]
    [DataRow(new[] { "--eprt", "--disable-eprt" }, false)]
    [DataRow(new[] { "--disable-eprt", "--eprt" }, true)]
    public void Parse_DisableEprtEprtAndTheirNegations_TheLaterWins(string[] flags, bool expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. flags, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.FtpUseEprt);
    }

    [TestMethod]
    [DataRow("--ssl")]
    [DataRow("--ftp-ssl")]
    public void Parse_Ssl_TriesTheUpgradeAndWarnsAsCurlDoes(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TransportSecurityLevel.Try, result.Options.SslLevel);
        CollectionAssert.AreEqual(
            new[] { $"Warning: {option} is an insecure option, consider --ssl-reqd instead" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SslWithValue_WarnsWithTheLongNameOnly()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ssl=x", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: --ssl is an insecure option, consider --ssl-reqd instead" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentThenSsl_DropsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--ssl", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TransportSecurityLevel.Try, result.Options.SslLevel);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SslThenSilent_KeepsTheWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ssl", "-s", Url]);

        Assert.HasCount(1, result.WarningLines);
    }

    [TestMethod]
    [DataRow("--no-ssl")]
    [DataRow("--no-ftp-ssl")]
    public void Parse_NegatedSsl_DoesNotWarn(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TransportSecurityLevel.None, result.Options.SslLevel);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(new[] { "--ssl-reqd" }, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ftp-ssl-reqd" }, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ssl-reqd", "--no-ssl-reqd" }, TransportSecurityLevel.None)]
    [DataRow(new[] { "--ftp-ssl-reqd", "--no-ftp-ssl-reqd" }, TransportSecurityLevel.None)]
    [DataRow(new[] { "--ssl", "--no-ssl" }, TransportSecurityLevel.None)]
    [DataRow(new[] { "--ftp-ssl", "--no-ssl" }, TransportSecurityLevel.None)]
    [DataRow(new[] { "--ssl-reqd", "--no-ssl" }, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ssl", "--no-ssl-reqd" }, TransportSecurityLevel.Try)]
    [DataRow(new[] { "--ssl", "--ssl-reqd" }, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ssl-reqd", "--ssl" }, TransportSecurityLevel.Required)]
    public void Parse_SslAndSslReqdAndTheirNegations_AreIndependentAndRequiredWins(string[] flags, TransportSecurityLevel expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. flags, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.SslLevel);
    }

    [TestMethod]
    [DataRow(new[] { "--ftp-ssl-control" }, true, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ftp-ssl-control", "--no-ftp-ssl-control" }, false, TransportSecurityLevel.None)]
    [DataRow(new[] { "--no-ftp-ssl-control", "--ftp-ssl-control" }, true, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ssl", "--ftp-ssl-control" }, true, TransportSecurityLevel.Required)]
    [DataRow(new[] { "--ftp-ssl-control", "--ssl-reqd" }, true, TransportSecurityLevel.Required)]
    public void Parse_FtpSslControlAndItsNegation_RequiresTlsOnTheControlConnection(string[] flags, bool expectedControlOnly, TransportSecurityLevel expectedLevel)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. flags, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedControlOnly, result.Options.FtpSslControlOnly);
        Assert.AreEqual(expectedLevel, result.Options.SslLevel);
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
