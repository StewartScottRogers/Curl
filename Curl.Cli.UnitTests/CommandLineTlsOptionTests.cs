using System.Security.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the TLS options: <c>-k</c>/<c>--insecure</c>, <c>--cacert</c>,
/// <c>--capath</c>, <c>-E</c>/<c>--cert</c>, <c>--key</c>, <c>--cert-type</c>, <c>--key-type</c>,
/// <c>--pass</c>, <c>--tlsv1.2</c>/<c>--tlsv1.3</c> (the
/// last one given wins), <c>--ciphers</c> and <c>--tls13-ciphers</c>, and the refusals curl
/// 8.21.0 prints for them, measured against the local curl 8.21.0 on 2026-09-26. The
/// <c>--cacert</c> existence check runs against a fake, never the disk, except in the two
/// <c>Integration</c> tests that pin the production check against real paths.
/// </summary>
[TestClass]
public sealed class CommandLineTlsOptionTests
{
    private const string Url = "https://example.com/";

    private static readonly Func<string, bool> EveryPathExists = _ => true;

    private static readonly Func<string, bool> NoPathExists = _ => false;

    [TestMethod]
    public void Parse_NoTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Insecure);
        Assert.IsNull(result.Options.CaCertificateFile);
        Assert.IsNull(result.Options.CaCertificateDirectory);
        Assert.IsNull(result.Options.ClientCertificate);
        Assert.IsNull(result.Options.PrivateKey);
        Assert.IsNull(result.Options.MinimumTlsVersion);
        Assert.IsNull(result.Options.Ciphers);
        Assert.IsNull(result.Options.Tls13Ciphers);
    }

    [TestMethod]
    [DataRow("-k")]
    [DataRow("--insecure")]
    public void Parse_Insecure_SetsInsecure(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Insecure);
    }

    [TestMethod]
    public void Parse_InsecureBundledWithSilent_SetsBoth()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sk", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Insecure);
        Assert.IsTrue(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_CacertThatExists_RecordsCaCertificateFile()
    {
        string? checkedPath = null;

        CommandLineParseResult result = CommandLineParser.Parse(
            ["--cacert", "ca.pem", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("ca.pem", result.Options.CaCertificateFile);
        Assert.AreEqual("ca.pem", checkedPath);
    }

    [TestMethod]
    public void Parse_CacertThatDoesNotExist_RefusesWithThreeLines()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", "nonexist.pem", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file 'nonexist.pem' provided to --cacert does not exist",
            "curl: option --cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_EmptyCacert_RefusesAsMissingFileNotAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", "", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '' provided to --cacert does not exist",
            "curl: option --cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_EmptyAttachedCacert_NamesTheOptionAsTyped()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cacert=", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '' provided to --cacert does not exist",
            "curl: option --cacert=: is badly used here");
    }

    [TestMethod]
    public void Parse_DefaultCheckGivenEmptyCacert_Refuses()
    {
        // Path.Exists("") answers false without touching the disk.
        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", "", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.HasCount(3, result.Refusal.StandardErrorLines);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_DefaultCheckGivenAnExistingFile_RecordsIt()
    {
        string existingFile = typeof(CommandLineTlsOptionTests).Assembly.Location;

        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", existingFile, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(existingFile, result.Options.CaCertificateFile);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_DefaultCheckGivenAnExistingDirectory_RecordsIt()
    {
        string existingDirectory = AppContext.BaseDirectory;

        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", existingDirectory, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(existingDirectory, result.Options.CaCertificateFile);
    }

    [TestMethod]
    public void Parse_Capath_RecordsCaCertificateDirectory()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--capath", "certs", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("certs", result.Options.CaCertificateDirectory);
    }

    [TestMethod]
    [DataRow("-E")]
    [DataRow("--cert")]
    public void Parse_Cert_RecordsClientCertificateVerbatim(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "client.pem:secret", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("client.pem:secret", result.Options.ClientCertificate);
    }

    [TestMethod]
    public void Parse_Key_RecordsPrivateKey()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--key", "client.key", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("client.key", result.Options.PrivateKey);
    }

    [TestMethod]
    public void Parse_CertType_RecordsClientCertificateTypeVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cert-type", "p12", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("p12", result.Options.ClientCertificateType);
    }

    [TestMethod]
    public void Parse_CertTypeTwice_TheLastWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cert-type", "DER", "--cert-type", "PEM", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("PEM", result.Options.ClientCertificateType);
    }

    [TestMethod]
    public void Parse_KeyType_RecordsPrivateKeyTypeVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--key-type", "der", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("der", result.Options.PrivateKeyType);
    }

    [TestMethod]
    public void Parse_KeyTypeTwice_TheLastWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--key-type", "DER", "--key-type", "PEM", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("PEM", result.Options.PrivateKeyType);
    }

    [TestMethod]
    public void Parse_Pass_RecordsPassphraseVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--pass", "s3cret:with colon", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("s3cret:with colon", result.Options.Passphrase);
    }

    [TestMethod]
    public void Parse_PassTwice_TheLastWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--pass", "first", "--pass", "second", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("second", result.Options.Passphrase);
    }

    [TestMethod]
    [DataRow("--cert-type")]
    [DataRow("--key-type")]
    [DataRow("--pass")]
    public void Parse_TypeOrPassGivenFlagLikeValue_AcceptsWithoutWarning(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "-x", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_Tlsv12_SetsMinimumTlsVersionTo12()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tlsv1.2", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls12, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv13_SetsMinimumTlsVersionTo13()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tlsv1.3", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls13, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv13ThenTlsv12_TheLastWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tlsv1.3", "--tlsv1.2", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls12, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv12ThenTlsv13_TheLastWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tlsv1.2", "--tlsv1.3", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls13, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Ciphers_RecordsCiphersVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ciphers", "ECDHE-RSA-AES128-GCM-SHA256:AES256-SHA", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("ECDHE-RSA-AES128-GCM-SHA256:AES256-SHA", result.Options.Ciphers);
    }

    [TestMethod]
    public void Parse_Tls13Ciphers_RecordsTls13CiphersVerbatim()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tls13-ciphers", "TLS_AES_128_GCM_SHA256", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("TLS_AES_128_GCM_SHA256", result.Options.Tls13Ciphers);
    }

    [TestMethod]
    [DataRow("--capath")]
    [DataRow("--cert")]
    [DataRow("-E")]
    [DataRow("--key")]
    [DataRow("--ciphers")]
    [DataRow("--tls13-ciphers")]
    [DataRow("--cert-type")]
    [DataRow("--key-type")]
    [DataRow("--pass")]
    public void Parse_EmptyTextValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "", Url], EveryPathExists);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--cert")]
    [DataRow("-E")]
    [DataRow("--key")]
    [DataRow("--capath")]
    public void Parse_FileNameOptionGivenFlagLikeValue_AcceptsWithFileNameWarning(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "-x", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_CiphersGivenFlagLikeValue_AcceptsWithoutWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ciphers", "-x", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.Ciphers);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_CacertGivenFlagLikeFileThatExists_AcceptsWithFileNameWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", "-x", Url], EveryPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.CaCertificateFile);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_CacertGivenFlagLikeFileThatDoesNotExist_RefusesAfterFileNameWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--cacert", "-x", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '-x' provided to --cacert does not exist",
            "curl: option --cacert: is badly used here");
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    private static void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
