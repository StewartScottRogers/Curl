using System.Security.Authentication;
using Curl.Protocol.Abstractions;

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

    private static readonly Func<string, bool> EveryPathExists = _ => true;

    private static readonly Func<string, bool> NoPathExists = _ => false;

    [TestMethod]
    public void Parse_NoTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Insecure);
    }

    [TestMethod]
    public void Parse_SslNoRevoke_SetsSkipRevocationCheck()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ssl-no-revoke", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.SkipRevocationCheck);
    }

    [TestMethod]
    public void Parse_SslNoRevokeThenNoSslNoRevoke_ChecksRevocation()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--ssl-no-revoke", "--no-ssl-no-revoke", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.SkipRevocationCheck);
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
    [DataRow("-1", ObsoleteTlsProtocols.Tls10)]
    [DataRow("--tlsv1", ObsoleteTlsProtocols.Tls10)]
    [DataRow("--tlsv1.0", ObsoleteTlsProtocols.Tls10)]
    [DataRow("--tlsv1.1", ObsoleteTlsProtocols.Tls11)]
    [DataRow("--tlsv1.2", SslProtocols.Tls12)]
    [DataRow("--tlsv1.3", SslProtocols.Tls13)]
    public void Parse_MinimumTlsVersionSpelling_SetsMinimumTlsVersion(string spelledOption, SslProtocols expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.MinimumTlsVersion);
        Assert.IsNull(result.Options.ProxyMinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_OneInAShortBundle_SetsMinimumTlsVersionTo10()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s1S", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.Silent);
        Assert.IsTrue(result.Options.ShowError);
        Assert.AreEqual(ObsoleteTlsProtocols.Tls10, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_Tlsv13ThenOne_TheLastWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tlsv1.3", "-1", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ObsoleteTlsProtocols.Tls10, result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_ProxyTlsv1_SetsProxyMinimumTlsVersionTo10AndLeavesTheOriginAlone()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-tlsv1", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ObsoleteTlsProtocols.Tls10, result.Options.ProxyMinimumTlsVersion);
        Assert.IsNull(result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_NoTlsVersionOptions_LeavesMaximumAndProxyMinimumNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse(["--tls-max", version, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.MaximumTlsVersion);
        Assert.IsNull(result.Options.MinimumTlsVersion);
    }

    [TestMethod]
    public void Parse_TlsMaxAttachedValue_SetsMaximumTlsVersion()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tls-max=1.2", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(SslProtocols.Tls12, result.Options.MaximumTlsVersion);
    }

    [TestMethod]
    public void Parse_TlsMaxDefaultAfterAVersion_ClearsTheMaximum()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tls-max", "1.2", "--tls-max", "default", Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse(["--tls-max", version, Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([minimumOption, "--tls-max", maximum, Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse(["--tls-max", maximum, minimumOption, Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([silent, first, second, third, Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([first, second, third, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public void Parse_TlsMaxWithNoValue_RefusesAsRequiringAParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--tls-max"], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url], NoPathExists);

        AssertRefused(result, $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix");
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

    [TestMethod]
    public void Parse_NoProxyTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-k", "--cacert", "ca.pem", "--capath", "certs", Url], EveryPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ProxyInsecure);
        Assert.IsNull(result.Options.ProxyCaCertificateFile);
        Assert.IsNull(result.Options.ProxyCaCertificateDirectory);
    }

    [TestMethod]
    public void Parse_ProxyInsecure_SetsProxyInsecureOnly()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-insecure", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ProxyInsecure);
        Assert.IsFalse(result.Options.Insecure);
    }

    [TestMethod]
    public void Parse_ProxyInsecureThenNoProxyInsecure_VerifiesTheProxy()
    {
        // curl -s -S --proxy-insecure --no-proxy-insecure -x https://localhost:18462 https://example.com/
        // against a self-signed proxy -> exit 60, as without either (curl 8.21.0, 2026-09-27).
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-insecure", "--no-proxy-insecure", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ProxyInsecure);
    }

    [TestMethod]
    public void Parse_ProxyCacertThatExists_RecordsProxyCaCertificateFileOnly()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-cacert", "proxy.pem", Url], EveryPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-cacert", file, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --proxy-cacert does not exist",
            "curl: option --proxy-cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_ProxyCapath_RecordsProxyCaCertificateDirectoryOnly()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-capath", "certs", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("certs", result.Options.ProxyCaCertificateDirectory);
        Assert.IsNull(result.Options.CaCertificateDirectory);
    }

    [TestMethod]
    public void Parse_EmptyProxyCapath_RefusesAsBlank()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-capath", "", Url], NoPathExists);

        AssertRefused(result, "curl: option --proxy-capath: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_ProxyCapathGivenFlagLikeValue_AcceptsWithFileNameWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-capath", "-x", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
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
