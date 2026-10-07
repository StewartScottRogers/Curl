using System.Security.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the TLS options: <c>-k</c>/<c>--insecure</c>, <c>--cacert</c>,
/// <c>--capath</c>, <c>-E</c>/<c>--cert</c>, <c>--key</c>, <c>--cert-type</c>, <c>--key-type</c>,
/// <c>--pass</c>, the minimum versions <c>-1</c>/<c>--tlsv1</c>, <c>--tlsv1.0</c>, <c>--tlsv1.1</c>,
/// <c>--tlsv1.2</c> and <c>--tlsv1.3</c> (the last one given wins), <c>--tls-max</c>, <c>--proxy-tlsv1</c>,
/// <c>--ciphers</c> and <c>--tls13-ciphers</c>, and the refusals curl
/// 8.21.0 prints for them, measured against the local curl 8.21.0 on 2026-09-26. The
/// <c>--cacert</c> existence check runs against a fake, never the disk, except in the two
/// <c>Integration</c> tests that pin the production check against real paths.
/// </summary>
[TestClass]
public sealed class CommandLineTlsOptionTests
{
    private const string Url = "https://example.com/";

    private const string FlagLikeFileNameWarning = "Warning: The filename argument '-x' looks like a flag.";

    private static readonly Func<string, bool> EveryPathExists = _ => true;

    private static readonly Func<string, bool> NoPathExists = _ => false;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("insecure", false, Recorded(result)?.Insecure);
        Diagnostics.Assert("CA certificate file", null, Recorded(result)?.CaCertificateFile);
        Diagnostics.Assert("minimum TLS version", null, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Insecure);
        Assert.IsFalse(result.Options.SkipRevocationCheck);
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
        CommandLineParseResult result = Parse([spelledOption, Url], NoPathExists);

        Diagnostics.Assert("insecure", true, Recorded(result)?.Insecure);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Insecure);
    }

    [TestMethod]
    public void Parse_SslNoRevoke_SetsSkipRevocationCheck()
    {
        CommandLineParseResult result = Parse(["--ssl-no-revoke", Url], NoPathExists);

        Diagnostics.Assert("skip revocation check", true, Recorded(result)?.SkipRevocationCheck);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.SkipRevocationCheck);
    }

    [TestMethod]
    public void Parse_SslNoRevokeThenNoSslNoRevoke_ChecksRevocation()
    {
        CommandLineParseResult result = Parse(["--ssl-no-revoke", "--no-ssl-no-revoke", Url], NoPathExists);

        Diagnostics.Assert("skip revocation check", false, Recorded(result)?.SkipRevocationCheck);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.SkipRevocationCheck);
    }

    [TestMethod]
    public void Parse_InsecureBundledWithSilent_SetsBoth()
    {
        CommandLineParseResult result = Parse(["-sk", Url], NoPathExists);

        Diagnostics.Assert("insecure", true, Recorded(result)?.Insecure);
        Diagnostics.Assert("silent", true, Recorded(result)?.Silent);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Insecure);
        Assert.IsTrue(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_CacertThatExists_RecordsCaCertificateFile()
    {
        string? checkedPath = null;

        CommandLineParseResult result = Parse(
            ["--cacert", "ca.pem", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Diagnostics.Act("checked path", checkedPath);
        Diagnostics.Assert("CA certificate file", "ca.pem", Recorded(result)?.CaCertificateFile);
        Diagnostics.Assert("checked path", "ca.pem", checkedPath);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("ca.pem", result.Options.CaCertificateFile);
        Assert.AreEqual("ca.pem", checkedPath);
    }

    [TestMethod]
    public void Parse_CacertThatDoesNotExist_RefusesWithThreeLines()
    {
        CommandLineParseResult result = Parse(["--cacert", "nonexist.pem", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file 'nonexist.pem' provided to --cacert does not exist",
            "curl: option --cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_EmptyCacert_RefusesAsMissingFileNotAsBlank()
    {
        CommandLineParseResult result = Parse(["--cacert", "", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '' provided to --cacert does not exist",
            "curl: option --cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_EmptyAttachedCacert_NamesTheOptionAsTyped()
    {
        CommandLineParseResult result = Parse(["--cacert=", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '' provided to --cacert does not exist",
            "curl: option --cacert=: is badly used here");
    }

    [TestMethod]
    public void Parse_DefaultCheckGivenEmptyCacert_Refuses()
    {
        // Path.Exists("") answers false without touching the disk.
        CommandLineParseResult result = Parse(["--cacert", "", Url], pathExists: null);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("stderr line count", 3, CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines.Count);
        Assert.IsFalse(result.IsAccepted);
        Assert.HasCount(3, result.Refusal.StandardErrorLines);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_DefaultCheckGivenAnExistingFile_RecordsIt()
    {
        string existingFile = typeof(CommandLineTlsOptionTests).Assembly.Location;

        CommandLineParseResult result = Parse(["--cacert", existingFile, Url], pathExists: null);

        Diagnostics.Assert("CA certificate file", existingFile, Recorded(result)?.CaCertificateFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(existingFile, result.Options.CaCertificateFile);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_DefaultCheckGivenAnExistingDirectory_RecordsIt()
    {
        string existingDirectory = AppContext.BaseDirectory;

        CommandLineParseResult result = Parse(["--cacert", existingDirectory, Url], pathExists: null);

        Diagnostics.Assert("CA certificate file", existingDirectory, Recorded(result)?.CaCertificateFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(existingDirectory, result.Options.CaCertificateFile);
    }

    [TestMethod]
    public void Parse_Capath_RecordsCaCertificateDirectory()
    {
        CommandLineParseResult result = Parse(["--capath", "certs", Url], NoPathExists);

        Diagnostics.Assert("CA certificate directory", "certs", Recorded(result)?.CaCertificateDirectory);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("certs", result.Options.CaCertificateDirectory);
    }

    [TestMethod]
    [DataRow("-E")]
    [DataRow("--cert")]
    public void Parse_Cert_RecordsClientCertificateVerbatim(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "client.pem:secret", Url], NoPathExists);

        Diagnostics.Assert("client certificate", "client.pem:secret", Recorded(result)?.ClientCertificate);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("client.pem:secret", result.Options.ClientCertificate);
    }

    [TestMethod]
    public void Parse_Key_RecordsPrivateKey()
    {
        CommandLineParseResult result = Parse(["--key", "client.key", Url], NoPathExists);

        Diagnostics.Assert("private key", "client.key", Recorded(result)?.PrivateKey);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("client.key", result.Options.PrivateKey);
    }

    [TestMethod]
    public void Parse_CertType_RecordsClientCertificateTypeVerbatim()
    {
        CommandLineParseResult result = Parse(["--cert-type", "p12", Url], NoPathExists);

        Diagnostics.Assert("client certificate type", "p12", Recorded(result)?.ClientCertificateType);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("p12", result.Options.ClientCertificateType);
    }

    [TestMethod]
    public void Parse_CertTypeTwice_TheLastWins()
    {
        CommandLineParseResult result = Parse(["--cert-type", "DER", "--cert-type", "PEM", Url], NoPathExists);

        Diagnostics.Assert("client certificate type", "PEM", Recorded(result)?.ClientCertificateType);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("PEM", result.Options.ClientCertificateType);
    }

    [TestMethod]
    public void Parse_KeyType_RecordsPrivateKeyTypeVerbatim()
    {
        CommandLineParseResult result = Parse(["--key-type", "der", Url], NoPathExists);

        Diagnostics.Assert("private key type", "der", Recorded(result)?.PrivateKeyType);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("der", result.Options.PrivateKeyType);
    }

    [TestMethod]
    public void Parse_KeyTypeTwice_TheLastWins()
    {
        CommandLineParseResult result = Parse(["--key-type", "DER", "--key-type", "PEM", Url], NoPathExists);

        Diagnostics.Assert("private key type", "PEM", Recorded(result)?.PrivateKeyType);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("PEM", result.Options.PrivateKeyType);
    }

    [TestMethod]
    public void Parse_Pass_RecordsPassphraseVerbatim()
    {
        CommandLineParseResult result = Parse(["--pass", "s3cret:with colon", Url], NoPathExists);

        Diagnostics.Assert("passphrase", "s3cret:with colon", Recorded(result)?.Passphrase);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("s3cret:with colon", result.Options.Passphrase);
    }

    [TestMethod]
    public void Parse_PassTwice_TheLastWins()
    {
        CommandLineParseResult result = Parse(["--pass", "first", "--pass", "second", Url], NoPathExists);

        Diagnostics.Assert("passphrase", "second", Recorded(result)?.Passphrase);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("second", result.Options.Passphrase);
    }

    [TestMethod]
    [DataRow("--cert-type")]
    [DataRow("--key-type")]
    [DataRow("--pass")]
    public void Parse_TypeOrPassGivenFlagLikeValue_AcceptsWithoutWarning(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "-x", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_Tlsv12_SetsMinimumTlsVersionTo12()
    {
        CommandLineParseResult result = Parse(["--tlsv1.2", Url], NoPathExists);

        Diagnostics.Assert("minimum TLS version", SslProtocols.Tls12, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls12, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv13_SetsMinimumTlsVersionTo13()
    {
        CommandLineParseResult result = Parse(["--tlsv1.3", Url], NoPathExists);

        Diagnostics.Assert("minimum TLS version", SslProtocols.Tls13, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls13, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv13ThenTlsv12_TheLastWins()
    {
        CommandLineParseResult result = Parse(["--tlsv1.3", "--tlsv1.2", Url], NoPathExists);

        Diagnostics.Assert("minimum TLS version", SslProtocols.Tls12, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls12, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv12ThenTlsv13_TheLastWins()
    {
        CommandLineParseResult result = Parse(["--tlsv1.2", "--tlsv1.3", Url], NoPathExists);

        Diagnostics.Assert("minimum TLS version", SslProtocols.Tls13, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls13, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    [DataRow("-1", ObsoleteTlsProtocols.Tls10)]
    [DataRow("--tlsv1", ObsoleteTlsProtocols.Tls10)]
    [DataRow("--tlsv1.0", ObsoleteTlsProtocols.Tls10)]
    [DataRow("--tlsv1.1", ObsoleteTlsProtocols.Tls11)]
    [DataRow("--tlsv1.2", SslProtocols.Tls12)]
    [DataRow("--tlsv1.3", SslProtocols.Tls13)]
    public void Parse_MinimumTlsVersionSpelling_SetsMinimumTlsVersion(string spelledOption, SslProtocols expected)
    {
        CommandLineParseResult result = Parse([spelledOption, Url], NoPathExists);

        Diagnostics.Assert("minimum TLS version", expected, Recorded(result)?.MinimumTlsVersion);
        Diagnostics.Assert("proxy minimum TLS version", null, Recorded(result)?.ProxyMinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.MinimumTlsVersion);
        Assert.IsNull(result.Options.ProxyMinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_OneInAShortBundle_SetsMinimumTlsVersionTo10()
    {
        CommandLineParseResult result = Parse(["-s1S", Url], NoPathExists);

        Diagnostics.Assert("silent", true, Recorded(result)?.Silent);
        Diagnostics.Assert("show error", true, Recorded(result)?.ShowError);
        Diagnostics.Assert("minimum TLS version", ObsoleteTlsProtocols.Tls10, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
        Assert.AreEqual(ObsoleteTlsProtocols.Tls10, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv13ThenOne_TheLastWins()
    {
        CommandLineParseResult result = Parse(["--tlsv1.3", "-1", Url], NoPathExists);

        Diagnostics.Assert("minimum TLS version", ObsoleteTlsProtocols.Tls10, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ObsoleteTlsProtocols.Tls10, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_ProxyTlsv1_SetsProxyMinimumTlsVersionTo10AndLeavesTheOriginAlone()
    {
        CommandLineParseResult result = Parse(["--proxy-tlsv1", Url], NoPathExists);

        Diagnostics.Assert("proxy minimum TLS version", ObsoleteTlsProtocols.Tls10, Recorded(result)?.ProxyMinimumTlsVersion);
        Diagnostics.Assert("minimum TLS version", null, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ObsoleteTlsProtocols.Tls10, result.Options.ProxyMinimumTlsVersion);
        Assert.IsNull(result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_NoTlsVersionOptions_LeavesMaximumAndProxyMinimumNotGiven()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Diagnostics.Assert("maximum TLS version", null, Recorded(result)?.MaximumTlsVersion);
        Diagnostics.Assert("proxy minimum TLS version", null, Recorded(result)?.ProxyMinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.MaximumTlsVersion);
        Assert.IsNull(result.Options.ProxyMinimumTlsVersion);
    }

    [TestMethod]
    [DataRow("1.0", ObsoleteTlsProtocols.Tls10)]
    [DataRow("1.1", ObsoleteTlsProtocols.Tls11)]
    [DataRow("1.2", SslProtocols.Tls12)]
    [DataRow("1.3", SslProtocols.Tls13)]
    public void Parse_TlsMaxVersion_SetsMaximumTlsVersion(string version, SslProtocols expected)
    {
        CommandLineParseResult result = Parse(["--tls-max", version, Url], NoPathExists);

        Diagnostics.Assert("maximum TLS version", expected, Recorded(result)?.MaximumTlsVersion);
        Diagnostics.Assert("minimum TLS version", null, Recorded(result)?.MinimumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.MaximumTlsVersion);
        Assert.IsNull(result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_TlsMaxAttachedValue_SetsMaximumTlsVersion()
    {
        CommandLineParseResult result = Parse(["--tls-max=1.2", Url], NoPathExists);

        Diagnostics.Assert("maximum TLS version", SslProtocols.Tls12, Recorded(result)?.MaximumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls12, result.Options.MaximumTlsVersion);
    }

    [TestMethod]
    public void Parse_TlsMaxDefaultAfterAVersion_ClearsTheMaximum()
    {
        CommandLineParseResult result = Parse(["--tls-max", "1.2", "--tls-max", "default", Url], NoPathExists);

        Diagnostics.Assert("maximum TLS version", null, Recorded(result)?.MaximumTlsVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.MaximumTlsVersion);
    }

    [TestMethod]
    [DataRow("1.4")]
    [DataRow("abc")]
    [DataRow("DEFAULT")]
    [DataRow("1")]
    [DataRow("1.2 ")]
    [DataRow("")]
    public void Parse_TlsMaxUnknownVersion_RefusesAsBadlyUsed(string version)
    {
        // Measured with Record-CurlExchange.ps1 -NoServer against curl 8.21.0 (Schannel) on 2026-09-28.
        CommandLineParseResult result = Parse(["--tls-max", version, Url], NoPathExists);

        AssertRefused(result, "curl: option --tls-max: is badly used here");
    }

    [TestMethod]
    [DataRow("--tlsv1.3", "1.2")]
    [DataRow("--tlsv1.2", "1.1")]
    [DataRow("--tlsv1.1", "1.0")]
    [DataRow("--tlsv1.3", "default")]
    [DataRow("--tlsv1.2", "default")]
    [DataRow("--tlsv1.0", "default")]
    [DataRow("-1", "default")]
    [DataRow("--tlsv1", "default")]
    public void Parse_TlsMaxBelowTheMinimumReadBefore_RefusesTheTlsMax(string minimumOption, string maximum)
    {
        // Measured with curl 8.21.0 (Schannel) and 8.18.0 (OpenSSL) on 2026-09-28 (BL-502 Notes).
        CommandLineParseResult result = Parse([minimumOption, "--tls-max", maximum, Url], NoPathExists);

        AssertRefused(
            result,
            "curl: --tls-max set lower than minimum accepted version",
            "curl: option --tls-max: is badly used here");
    }

    [TestMethod]
    [DataRow("1.2", "--tlsv1.3")]
    [DataRow("1.1", "--tlsv1.2")]
    [DataRow("1.0", "--tlsv1.1")]
    public void Parse_MinimumAboveTheTlsMaxReadBefore_RefusesTheMinimum(string maximum, string minimumOption)
    {
        // Measured with curl 8.21.0 (Schannel) on 2026-09-28 (BL-502 Notes).
        CommandLineParseResult result = Parse(["--tls-max", maximum, minimumOption, Url], NoPathExists);

        AssertRefused(
            result,
            "curl: Minimum TLS version set higher than max",
            $"curl: option {minimumOption}: is badly used here");
    }

    [TestMethod]
    [DataRow("-s", "--tlsv1.3", "--tls-max", "1.2", "curl: option --tls-max: is badly used here")]
    [DataRow("-s", "--tls-max", "1.2", "--tlsv1.3", "curl: option --tlsv1.3: is badly used here")]
    public void Parse_TlsVersionRangeRefusedWhileSilent_HidesTheFirstLine(string silent, string first, string second, string third, string expectedLine)
    {
        CommandLineParseResult result = Parse([silent, first, second, third, Url], NoPathExists);

        AssertRefused(result, expectedLine);
    }

    [TestMethod]
    [DataRow("--tlsv1.3", "--tls-max", "1.3")]
    [DataRow("--tls-max", "1.3", "--tlsv1.3")]
    [DataRow("--tlsv1.2", "--tls-max", "1.2")]
    [DataRow("-1", "--tls-max", "1.0")]
    [DataRow("--tls-max", "1.0", "--tlsv1")]
    [DataRow("--tls-max", "default", "--tlsv1.3")]
    [DataRow("--proxy-tlsv1", "--tls-max", "default")]
    public void Parse_TlsVersionRangeThatIsNotEmpty_IsAccepted(string first, string second, string third)
    {
        // Measured with curl 8.21.0 (Schannel) on 2026-09-28 (BL-502 Notes): each reaches the connect.
        CommandLineParseResult result = Parse([first, second, third, Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public void Parse_TlsMaxWithNoValue_RefusesAsRequiringAParameter()
    {
        CommandLineParseResult result = Parse(["--tls-max"], NoPathExists);

        AssertRefused(result, "curl: option --tls-max: requires parameter");
    }

    [TestMethod]
    [DataRow("--no-tlsv1")]
    [DataRow("--no-tlsv1.0")]
    [DataRow("--no-tlsv1.1")]
    [DataRow("--no-tls-max")]
    [DataRow("--no-proxy-tlsv1")]
    public void Parse_NegatedTlsVersionOption_RefusesAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url], NoPathExists);

        AssertRefused(result, $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_Ciphers_RecordsCiphersVerbatim()
    {
        CommandLineParseResult result = Parse(["--ciphers", "ECDHE-RSA-AES128-GCM-SHA256:AES256-SHA", Url], NoPathExists);

        Diagnostics.Assert("ciphers", "ECDHE-RSA-AES128-GCM-SHA256:AES256-SHA", Recorded(result)?.Ciphers);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("ECDHE-RSA-AES128-GCM-SHA256:AES256-SHA", result.Options.Ciphers);
    }

    [TestMethod]
    public void Parse_Tls13Ciphers_RecordsTls13CiphersVerbatim()
    {
        CommandLineParseResult result = Parse(["--tls13-ciphers", "TLS_AES_128_GCM_SHA256", Url], NoPathExists);

        Diagnostics.Assert("TLS 1.3 ciphers", "TLS_AES_128_GCM_SHA256", Recorded(result)?.Tls13Ciphers);
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
        CommandLineParseResult result = Parse([spelledOption, "", Url], EveryPathExists);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--cert")]
    [DataRow("-E")]
    [DataRow("--key")]
    [DataRow("--capath")]
    public void Parse_FileNameOptionGivenFlagLikeValue_AcceptsWithFileNameWarning(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "-x", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines([FlagLikeFileNameWarning], result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_CiphersGivenFlagLikeValue_AcceptsWithoutWarning()
    {
        CommandLineParseResult result = Parse(["--ciphers", "-x", Url], NoPathExists);

        Diagnostics.Assert("ciphers", "-x", Recorded(result)?.Ciphers);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.Ciphers);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_CacertGivenFlagLikeFileThatExists_AcceptsWithFileNameWarning()
    {
        CommandLineParseResult result = Parse(["--cacert", "-x", Url], EveryPathExists);

        Diagnostics.Assert("CA certificate file", "-x", Recorded(result)?.CaCertificateFile);
        AssertWarningLines([FlagLikeFileNameWarning], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.CaCertificateFile);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_CacertGivenFlagLikeFileThatDoesNotExist_RefusesAfterFileNameWarning()
    {
        CommandLineParseResult result = Parse(["--cacert", "-x", Url], NoPathExists);

        AssertWarningLines([FlagLikeFileNameWarning], result);
        AssertRefused(
            result,
            "curl: The file '-x' provided to --cacert does not exist",
            "curl: option --cacert: is badly used here");
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoProxyTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse(["-k", "--cacert", "ca.pem", "--capath", "certs", Url], EveryPathExists);

        Diagnostics.Assert("proxy insecure", false, Recorded(result)?.ProxyInsecure);
        Diagnostics.Assert("proxy CA certificate file", null, Recorded(result)?.ProxyCaCertificateFile);
        Diagnostics.Assert("proxy CA certificate directory", null, Recorded(result)?.ProxyCaCertificateDirectory);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ProxyInsecure);
        Assert.IsNull(result.Options.ProxyCaCertificateFile);
        Assert.IsNull(result.Options.ProxyCaCertificateDirectory);
    }

    [TestMethod]
    public void Parse_ProxyInsecure_SetsProxyInsecureOnly()
    {
        CommandLineParseResult result = Parse(["--proxy-insecure", Url], NoPathExists);

        Diagnostics.Assert("proxy insecure", true, Recorded(result)?.ProxyInsecure);
        Diagnostics.Assert("insecure", false, Recorded(result)?.Insecure);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ProxyInsecure);
        Assert.IsFalse(result.Options.Insecure);
    }

    [TestMethod]
    public void Parse_ProxyInsecureThenNoProxyInsecure_VerifiesTheProxy()
    {
        // curl -s -S --proxy-insecure --no-proxy-insecure -x https://localhost:18462 https://example.com/
        // against a self-signed proxy -> exit 60, as without either (curl 8.21.0, 2026-09-27).
        CommandLineParseResult result = Parse(["--proxy-insecure", "--no-proxy-insecure", Url], NoPathExists);

        Diagnostics.Assert("proxy insecure", false, Recorded(result)?.ProxyInsecure);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ProxyInsecure);
    }

    [TestMethod]
    public void Parse_ProxyCacertThatExists_RecordsProxyCaCertificateFileOnly()
    {
        CommandLineParseResult result = Parse(["--proxy-cacert", "proxy.pem", Url], EveryPathExists);

        Diagnostics.Assert("proxy CA certificate file", "proxy.pem", Recorded(result)?.ProxyCaCertificateFile);
        Diagnostics.Assert("CA certificate file", null, Recorded(result)?.CaCertificateFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("proxy.pem", result.Options.ProxyCaCertificateFile);
        Assert.IsNull(result.Options.CaCertificateFile);
    }

    [TestMethod]
    [DataRow("nosuch.pem")]
    [DataRow("")]
    public void Parse_ProxyCacertThatDoesNotExist_RefusesNamingProxyCacert(string file)
    {
        // curl --proxy-cacert nosuch.pem -x http://127.0.0.1:1 http://127.0.0.1:1/ (and '') -> exit 2 (curl 8.21.0, 2026-09-27).
        CommandLineParseResult result = Parse(["--proxy-cacert", file, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --proxy-cacert does not exist",
            "curl: option --proxy-cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_ProxyCapath_RecordsProxyCaCertificateDirectoryOnly()
    {
        CommandLineParseResult result = Parse(["--proxy-capath", "certs", Url], NoPathExists);

        Diagnostics.Assert("proxy CA certificate directory", "certs", Recorded(result)?.ProxyCaCertificateDirectory);
        Diagnostics.Assert("CA certificate directory", null, Recorded(result)?.CaCertificateDirectory);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("certs", result.Options.ProxyCaCertificateDirectory);
        Assert.IsNull(result.Options.CaCertificateDirectory);
    }

    [TestMethod]
    public void Parse_EmptyProxyCapath_RefusesAsBlank()
    {
        CommandLineParseResult result = Parse(["--proxy-capath", "", Url], NoPathExists);

        AssertRefused(result, "curl: option --proxy-capath: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_ProxyCapathGivenFlagLikeValue_AcceptsWithFileNameWarning()
    {
        CommandLineParseResult result = Parse(["--proxy-capath", "-x", Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines([FlagLikeFileNameWarning], result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoRevocationOrPinningOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Diagnostics.Assert("certificate revocation list file", null, Recorded(result)?.CertificateRevocationListFile);
        Diagnostics.Assert("pinned public key", null, Recorded(result)?.PinnedPublicKey);
        Diagnostics.Assert("require certificate status", false, Recorded(result)?.RequireCertificateStatus);
        Diagnostics.Assert("auto client certificate", false, Recorded(result)?.AutoClientCertificate);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.CertificateRevocationListFile);
        Assert.IsNull(result.Options.PinnedPublicKey);
        Assert.IsFalse(result.Options.RequireCertificateStatus);
        Assert.IsFalse(result.Options.AutoClientCertificate);
    }

    [TestMethod]
    public void Parse_CrlfileThatExists_RecordsCertificateRevocationListFile()
    {
        string? checkedPath = null;

        CommandLineParseResult result = Parse(
            ["--crlfile", "revoked.crl", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Diagnostics.Act("checked path", checkedPath);
        Diagnostics.Assert("certificate revocation list file", "revoked.crl", Recorded(result)?.CertificateRevocationListFile);
        Diagnostics.Assert("checked path", "revoked.crl", checkedPath);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("revoked.crl", result.Options.CertificateRevocationListFile);
        Assert.AreEqual("revoked.crl", checkedPath);
    }

    [TestMethod]
    [DataRow("nosuch.crl")]
    [DataRow("")]
    public void Parse_CrlfileThatDoesNotExist_RefusesNamingCrlfile(string file)
    {
        // curl --crlfile '' file:///nonexist -> exit 2 with these lines (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--crlfile", file, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --crlfile does not exist",
            "curl: option --crlfile: is badly used here");
    }

    [TestMethod]
    public void Parse_CrlfileGivenFlagLikeValue_WarnsBeforeRefusing()
    {
        // curl --crlfile -zz file:///nonexist warns, then refuses with exit 2 (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--crlfile", "-zz", Url], NoPathExists);

        AssertWarningLines(["Warning: The filename argument '-zz' looks like a flag."], result);
        AssertRefused(
            result,
            "curl: The file '-zz' provided to --crlfile does not exist",
            "curl: option --crlfile: is badly used here");
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-zz' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoCrlfile_RefusesAsNotReversible()
    {
        // curl --no-crlfile x file:///nonexist -> exit 2 (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--no-crlfile", "x", Url], EveryPathExists);

        AssertRefused(result, "curl: option --no-crlfile: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("sha256//YhKJKSzoTt2b5FP18fvpHo7fJYqQCjAa3HWY3tvRMwE=")]
    [DataRow("sha256//YhKJKSzoTt2b5FP18fvpHo7fJYqQCjAa3HWY3tvRMwE=;sha256//t62CeU2tQiqkexU74Gxa2eg7fRbEgoChTociMee9wno=")]
    [DataRow("server.pub.pem")]
    [DataRow("-zz")]
    public void Parse_Pinnedpubkey_RecordsPinnedPublicKeyVerbatimWithoutWarning(string pins)
    {
        // curl --pinnedpubkey -zz file:///nonexist -> no warning, exit 37 from the file URL (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--pinnedpubkey", pins, Url], NoPathExists);

        Diagnostics.Assert("pinned public key", pins, Recorded(result)?.PinnedPublicKey);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(pins, result.Options.PinnedPublicKey);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_EmptyPinnedpubkey_RefusesAsBlank()
    {
        // curl --pinnedpubkey '' file:///nonexist -> exit 2 (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--pinnedpubkey", "", Url], NoPathExists);

        AssertRefused(result, "curl: option --pinnedpubkey: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_NoPinnedpubkey_RefusesAsNotReversible()
    {
        CommandLineParseResult result = Parse(["--no-pinnedpubkey", "x", Url], NoPathExists);

        AssertRefused(result, "curl: option --no-pinnedpubkey: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_CertStatus_SetsRequireCertificateStatus()
    {
        CommandLineParseResult result = Parse(["--cert-status", Url], NoPathExists);

        Diagnostics.Assert("require certificate status", true, Recorded(result)?.RequireCertificateStatus);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.RequireCertificateStatus);
    }

    [TestMethod]
    public void Parse_CertStatusThenNoCertStatus_DoesNotRequireCertificateStatus()
    {
        CommandLineParseResult result = Parse(["--cert-status", "--no-cert-status", Url], NoPathExists);

        Diagnostics.Assert("require certificate status", false, Recorded(result)?.RequireCertificateStatus);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.RequireCertificateStatus);
    }

    [TestMethod]
    public void Parse_SslAutoClientCert_SetsAutoClientCertificate()
    {
        CommandLineParseResult result = Parse(["--ssl-auto-client-cert", Url], NoPathExists);

        Diagnostics.Assert("auto client certificate", true, Recorded(result)?.AutoClientCertificate);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.AutoClientCertificate);
    }

    [TestMethod]
    public void Parse_SslAutoClientCertThenNoSslAutoClientCert_PicksNoClientCertificate()
    {
        // curl --cert-status --no-cert-status --ssl-auto-client-cert --no-ssl-auto-client-cert file:///nonexist
        // parses all four and exits 37 from the file URL (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--ssl-auto-client-cert", "--no-ssl-auto-client-cert", Url], NoPathExists);

        Diagnostics.Assert("auto client certificate", false, Recorded(result)?.AutoClientCertificate);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.AutoClientCertificate);
    }

    /// <summary>
    /// Returns the parsed options, or null for a refusal, for diagnostic lines written before the test asserts
    /// acceptance, without making the compiler treat <see cref="CommandLineParseResult.Options"/> as possibly null.
    /// </summary>
    private static CommandLineOptions? Recorded(CommandLineParseResult result) => result.Options;

    /// <summary>
    /// Parses <paramref name="arguments"/> with <paramref name="pathExists"/> as the file check, or the production
    /// check when it is null, writing the arguments, the outcome and the TLS option values as diagnostics.
    /// </summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool>? pathExists)
    {
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("path check", pathExists is null ? "production" : pathExists == EveryPathExists ? "every path exists" : pathExists == NoPathExists ? "no path exists" : "recording, every path exists");
        CommandLineParseResult result = pathExists is null ? CommandLineParser.Parse(arguments) : CommandLineParser.Parse(arguments, pathExists);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            WriteTlsOptions(result.Options);
        }

        return result;
    }

    private void WriteTlsOptions(CommandLineOptions options)
    {
        Diagnostics.Act("insecure", options.Insecure);
        Diagnostics.Act("skip revocation check", options.SkipRevocationCheck);
        Diagnostics.Act("CA certificate file", options.CaCertificateFile);
        Diagnostics.Act("CA certificate directory", options.CaCertificateDirectory);
        Diagnostics.Act("client certificate", options.ClientCertificate);
        Diagnostics.Act("client certificate type", options.ClientCertificateType);
        Diagnostics.Act("private key", options.PrivateKey);
        Diagnostics.Act("private key type", options.PrivateKeyType);
        Diagnostics.Act("passphrase", options.Passphrase);
        Diagnostics.Act("minimum TLS version", options.MinimumTlsVersion);
        Diagnostics.Act("maximum TLS version", options.MaximumTlsVersion);
        Diagnostics.Act("proxy minimum TLS version", options.ProxyMinimumTlsVersion);
        Diagnostics.Act("ciphers", options.Ciphers);
        Diagnostics.Act("TLS 1.3 ciphers", options.Tls13Ciphers);
        Diagnostics.Act("proxy insecure", options.ProxyInsecure);
        Diagnostics.Act("proxy CA certificate file", options.ProxyCaCertificateFile);
        Diagnostics.Act("proxy CA certificate directory", options.ProxyCaCertificateDirectory);
        Diagnostics.Act("certificate revocation list file", options.CertificateRevocationListFile);
        Diagnostics.Act("pinned public key", options.PinnedPublicKey);
        Diagnostics.Act("require certificate status", options.RequireCertificateStatus);
        Diagnostics.Act("auto client certificate", options.AutoClientCertificate);
    }

    private void AssertWarningLines(IEnumerable<string> expected, CommandLineParseResult result) =>
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
