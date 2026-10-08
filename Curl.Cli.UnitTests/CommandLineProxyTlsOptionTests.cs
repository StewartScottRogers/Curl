using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the HTTPS-proxy TLS options <c>--proxy-cert</c>, <c>--proxy-key</c>,
/// <c>--proxy-cert-type</c>, <c>--proxy-key-type</c>, <c>--proxy-pass</c>, <c>--proxy-ciphers</c>,
/// <c>--proxy-tls13-ciphers</c>, <c>--proxy-crlfile</c>, <c>--proxy-pinnedpubkey</c>, <c>--proxy-ca-native</c>,
/// <c>--proxy-ssl-auto-client-cert</c> and <c>--proxy-ssl-allow-beast</c>: each lands on its own proxy
/// property, as its origin counterpart does, and the two sets never touch each other. Measured against the
/// local curl 8.21.0 (Schannel) on 2026-09-28 (BL-605 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineProxyTlsOptionTests
{
    private const string Url = "https://example.com/";

    private static readonly Func<string, bool> EveryPathExists = _ => true;

    private static readonly Func<string, bool> NoPathExists = _ => false;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoProxyTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        AssertProxyTlsOptionsNotGiven(result.Options);
        Assert.IsNull(result.Options.ProxyClientCertificate);
        Assert.IsNull(result.Options.ProxyPrivateKey);
        Assert.IsNull(result.Options.ProxyClientCertificateType);
        Assert.IsNull(result.Options.ProxyPrivateKeyType);
        Assert.IsNull(result.Options.ProxyPassphrase);
        Assert.IsNull(result.Options.ProxyCiphers);
        Assert.IsNull(result.Options.ProxyTls13Ciphers);
        Assert.IsNull(result.Options.ProxyCertificateRevocationListFile);
        Assert.IsNull(result.Options.ProxyPinnedPublicKey);
        Assert.IsFalse(result.Options.ProxyUseNativeCaStore);
        Assert.IsFalse(result.Options.ProxyAutoClientCertificate);
        Assert.IsFalse(result.Options.ProxyAllowBeast);
    }

    [TestMethod]
    [DataRow("proxy.pem")]
    [DataRow("proxy.pem:secret")]
    [DataRow(@"proxy\:name.pem:se\:cret")]
    [DataRow(@"CurrentUser\MY\0123456789abcdef0123456789abcdef01234567")]
    public void Parse_ProxyCert_RecordsProxyClientCertificateVerbatimAndLeavesCertUnset(string certificate)
    {
        // The certificate[:password] split, escaped colons included, happens where it is applied (ADR-0066).
        CommandLineParseResult result = Parse(["--proxy-cert", certificate, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        AssertText("proxy client certificate", certificate, result.Options.ProxyClientCertificate);
        AssertText("client certificate", null, result.Options.ClientCertificate);
        Assert.AreEqual(certificate, result.Options.ProxyClientCertificate);
        Assert.IsNull(result.Options.ClientCertificate);
    }

    [TestMethod]
    [DataRow("--proxy-cert")]
    [DataRow("--proxy-key")]
    public void Parse_ProxyCertOrKeyGivenFlagLikeValue_WarnsAsAFileName(string option)
    {
        // curl --proxy-cert -zz file:///nosuch/zz warns, then exit 37 from the file URL (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse([option, "-zz", Url], NoPathExists);

        AssertWarnings(["Warning: The filename argument '-zz' looks like a flag."], result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-zz' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--proxy-cert-type")]
    [DataRow("--proxy-key-type")]
    [DataRow("--proxy-pass")]
    [DataRow("--proxy-ciphers")]
    [DataRow("--proxy-tls13-ciphers")]
    [DataRow("--proxy-pinnedpubkey")]
    public void Parse_ProxyTextOptionGivenFlagLikeValue_AcceptsWithoutWarning(string option)
    {
        // curl --proxy-pass -zz file:///nosuch/zz -> no filename warning, exit 37 (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse([option, "-zz", Url], NoPathExists);

        AssertWarnings([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0, result.WarningLines.Count());
    }

    [TestMethod]
    public void Parse_EveryProxyTextOption_RecordsItsProxyValueAndLeavesTheOriginOnesUnset()
    {
        CommandLineParseResult result = Parse(
            [
                "--proxy-key", "proxy.key",
                "--proxy-cert-type", "P12",
                "--proxy-key-type", "DER",
                "--proxy-pass", "phrase",
                "--proxy-ciphers", "ECDHE-RSA-AES128-GCM-SHA256",
                "--proxy-tls13-ciphers", "TLS_AES_128_GCM_SHA256",
                "--proxy-crlfile", "proxy.crl",
                "--proxy-pinnedpubkey", "sha256//abc=",
                Url,
            ],
            EveryPathExists);

        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions options = result.Options;
        AssertText("proxy private key", "proxy.key", options.ProxyPrivateKey);
        AssertText("proxy client certificate type", "P12", options.ProxyClientCertificateType);
        AssertText("proxy private key type", "DER", options.ProxyPrivateKeyType);
        AssertText("proxy passphrase", "phrase", options.ProxyPassphrase);
        AssertText("proxy ciphers", "ECDHE-RSA-AES128-GCM-SHA256", options.ProxyCiphers);
        AssertText("proxy TLS 1.3 ciphers", "TLS_AES_128_GCM_SHA256", options.ProxyTls13Ciphers);
        AssertText("proxy CRL file", "proxy.crl", options.ProxyCertificateRevocationListFile);
        AssertText("proxy pinned public key", "sha256//abc=", options.ProxyPinnedPublicKey);
        AssertText("private key", null, options.PrivateKey);
        AssertText("client certificate type", null, options.ClientCertificateType);
        AssertText("private key type", null, options.PrivateKeyType);
        AssertText("passphrase", null, options.Passphrase);
        AssertText("ciphers", null, options.Ciphers);
        AssertText("TLS 1.3 ciphers", null, options.Tls13Ciphers);
        AssertText("CRL file", null, options.CertificateRevocationListFile);
        AssertText("pinned public key", null, options.PinnedPublicKey);
        Assert.AreEqual("proxy.key", result.Options.ProxyPrivateKey);
        Assert.AreEqual("P12", result.Options.ProxyClientCertificateType);
        Assert.AreEqual("DER", result.Options.ProxyPrivateKeyType);
        Assert.AreEqual("phrase", result.Options.ProxyPassphrase);
        Assert.AreEqual("ECDHE-RSA-AES128-GCM-SHA256", result.Options.ProxyCiphers);
        Assert.AreEqual("TLS_AES_128_GCM_SHA256", result.Options.ProxyTls13Ciphers);
        Assert.AreEqual("proxy.crl", result.Options.ProxyCertificateRevocationListFile);
        Assert.AreEqual("sha256//abc=", result.Options.ProxyPinnedPublicKey);
        Assert.IsNull(result.Options.PrivateKey);
        Assert.IsNull(result.Options.ClientCertificateType);
        Assert.IsNull(result.Options.PrivateKeyType);
        Assert.IsNull(result.Options.Passphrase);
        Assert.IsNull(result.Options.Ciphers);
        Assert.IsNull(result.Options.Tls13Ciphers);
        Assert.IsNull(result.Options.CertificateRevocationListFile);
        Assert.IsNull(result.Options.PinnedPublicKey);
    }

    [TestMethod]
    public void Parse_EveryOriginTextOption_LeavesTheProxyOnesUnset()
    {
        CommandLineParseResult result = Parse(
            [
                "--cert", "client.pem:secret",
                "--key", "client.key",
                "--cert-type", "PEM",
                "--key-type", "PEM",
                "--pass", "phrase",
                "--ciphers", "AES256-SHA",
                "--tls13-ciphers", "TLS_AES_256_GCM_SHA384",
                "--crlfile", "origin.crl",
                "--pinnedpubkey", "sha256//xyz=",
                "--ca-native",
                "--ssl-auto-client-cert",
                "--ssl-allow-beast",
                Url,
            ],
            EveryPathExists);

        Assert.IsTrue(result.IsAccepted);
        AssertProxyTlsOptionsNotGiven(result.Options);
        Assert.IsNull(result.Options.ProxyClientCertificate);
        Assert.IsNull(result.Options.ProxyPrivateKey);
        Assert.IsNull(result.Options.ProxyClientCertificateType);
        Assert.IsNull(result.Options.ProxyPrivateKeyType);
        Assert.IsNull(result.Options.ProxyPassphrase);
        Assert.IsNull(result.Options.ProxyCiphers);
        Assert.IsNull(result.Options.ProxyTls13Ciphers);
        Assert.IsNull(result.Options.ProxyCertificateRevocationListFile);
        Assert.IsNull(result.Options.ProxyPinnedPublicKey);
        Assert.IsFalse(result.Options.ProxyUseNativeCaStore);
        Assert.IsFalse(result.Options.ProxyAutoClientCertificate);
        Assert.IsFalse(result.Options.ProxyAllowBeast);
    }

    [TestMethod]
    public void Parse_ProxyAndOriginCertificatesTogether_KeepEachOnItsOwnProperty()
    {
        CommandLineParseResult result = Parse(
            ["--cert", "client.pem:a", "--proxy-cert", "proxy.pem:b", "--pass", "origin", "--proxy-pass", "proxy", Url],
            NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        AssertText("client certificate", "client.pem:a", result.Options.ClientCertificate);
        AssertText("proxy client certificate", "proxy.pem:b", result.Options.ProxyClientCertificate);
        AssertText("passphrase", "origin", result.Options.Passphrase);
        AssertText("proxy passphrase", "proxy", result.Options.ProxyPassphrase);
        Assert.AreEqual("client.pem:a", result.Options.ClientCertificate);
        Assert.AreEqual("proxy.pem:b", result.Options.ProxyClientCertificate);
        Assert.AreEqual("origin", result.Options.Passphrase);
        Assert.AreEqual("proxy", result.Options.ProxyPassphrase);
    }

    [TestMethod]
    public void Parse_ProxyCrlfileThatExists_ChecksAndRecordsIt()
    {
        string? checkedPath = null;

        CommandLineParseResult result = Parse(
            ["--proxy-crlfile", "proxy.crl", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Diagnostics.Act("checked path", checkedPath);
        Assert.IsTrue(result.IsAccepted);
        AssertText("proxy CRL file", "proxy.crl", result.Options.ProxyCertificateRevocationListFile);
        AssertText("checked path", "proxy.crl", checkedPath);
        Assert.AreEqual("proxy.crl", result.Options.ProxyCertificateRevocationListFile);
        Assert.AreEqual("proxy.crl", checkedPath);
    }

    [TestMethod]
    public void Parse_ProxyCrlfileThatDoesNotExist_RefusesNamingProxyCrlfile()
    {
        // curl --proxy-crlfile nosuchfile.x file:///nosuch/zz -> exit 2 with these lines (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse(["--proxy-crlfile", "nosuchfile.x", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file 'nosuchfile.x' provided to --proxy-crlfile does not exist",
            "curl: option --proxy-crlfile: is badly used here");
    }

    [TestMethod]
    [DataRow("--proxy-ca-native", nameof(CommandLineOptions.ProxyUseNativeCaStore))]
    [DataRow("--proxy-ssl-auto-client-cert", nameof(CommandLineOptions.ProxyAutoClientCertificate))]
    [DataRow("--proxy-ssl-allow-beast", nameof(CommandLineOptions.ProxyAllowBeast))]
    public void Parse_ProxySwitch_SetsOnlyTheProxyProperty(string option, string property)
    {
        CommandLineParseResult result = Parse([option, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert(property, true, ReadSwitch(result.Options, property));
        Diagnostics.Assert("use native CA store", false, result.Options.UseNativeCaStore);
        Diagnostics.Assert("auto client certificate", false, result.Options.AutoClientCertificate);
        Diagnostics.Assert("allow beast", false, result.Options.AllowBeast);
        Assert.IsTrue(ReadSwitch(result.Options, property));
        Assert.IsFalse(result.Options.UseNativeCaStore);
        Assert.IsFalse(result.Options.AutoClientCertificate);
        Assert.IsFalse(result.Options.AllowBeast);
    }

    [TestMethod]
    [DataRow("--proxy-ca-native", nameof(CommandLineOptions.ProxyUseNativeCaStore))]
    [DataRow("--proxy-ssl-auto-client-cert", nameof(CommandLineOptions.ProxyAutoClientCertificate))]
    [DataRow("--proxy-ssl-allow-beast", nameof(CommandLineOptions.ProxyAllowBeast))]
    public void Parse_ProxySwitchThenItsNoForm_TurnsItOff(string option, string property)
    {
        // curl --no-proxy-ca-native file:///nosuch/zz -> accepted, exit 37 from the file URL (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse([option, "--no" + option[1..], Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert(property, false, ReadSwitch(result.Options, property));
        Assert.IsFalse(ReadSwitch(result.Options, property));
    }

    [TestMethod]
    [DataRow("--no-proxy-cert")]
    [DataRow("--no-proxy-key")]
    [DataRow("--no-proxy-cert-type")]
    [DataRow("--no-proxy-key-type")]
    [DataRow("--no-proxy-pass")]
    [DataRow("--no-proxy-ciphers")]
    [DataRow("--no-proxy-tls13-ciphers")]
    [DataRow("--no-proxy-crlfile")]
    [DataRow("--no-proxy-pinnedpubkey")]
    public void Parse_NoFormOfAProxyValueOption_RefusesAsNotReversible(string option)
    {
        // curl --no-proxy-cert x file:///nosuch/zz -> exit 2 (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = Parse([option, "x", Url], EveryPathExists);

        AssertRefused(result, $"curl: option {option}: the given option cannot be reversed with a --no- prefix");
    }

    private static bool ReadSwitch(CommandLineOptions options, string property) => property switch
    {
        nameof(CommandLineOptions.ProxyUseNativeCaStore) => options.ProxyUseNativeCaStore,
        nameof(CommandLineOptions.ProxyAutoClientCertificate) => options.ProxyAutoClientCertificate,
        _ => options.ProxyAllowBeast,
    };

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists)
    {
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("paths that exist", pathExists == NoPathExists ? "none" : "every path");
        CommandLineParseResult result = CommandLineParser.Parse(arguments, pathExists);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertText(string label, string? expected, string? actual) =>
        Diagnostics.Assert(label, Quote(expected), Quote(actual));

    private static string Quote(string? value) => value is null ? "null" : "\"" + value + "\"";

    private void AssertWarnings(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "warnings",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertProxyTlsOptionsNotGiven(CommandLineOptions options)
    {
        AssertText("proxy client certificate", null, options.ProxyClientCertificate);
        AssertText("proxy private key", null, options.ProxyPrivateKey);
        AssertText("proxy client certificate type", null, options.ProxyClientCertificateType);
        AssertText("proxy private key type", null, options.ProxyPrivateKeyType);
        AssertText("proxy passphrase", null, options.ProxyPassphrase);
        AssertText("proxy ciphers", null, options.ProxyCiphers);
        AssertText("proxy TLS 1.3 ciphers", null, options.ProxyTls13Ciphers);
        AssertText("proxy CRL file", null, options.ProxyCertificateRevocationListFile);
        AssertText("proxy pinned public key", null, options.ProxyPinnedPublicKey);
        Diagnostics.Assert("proxy use native CA store", false, options.ProxyUseNativeCaStore);
        Diagnostics.Assert("proxy auto client certificate", false, options.ProxyAutoClientCertificate);
        Diagnostics.Assert("proxy allow beast", false, options.ProxyAllowBeast);
    }

    private void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        CommandLineRefusal? refusal = CommandLineParseDiagnostics.Peek(result.Refusal);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine)),
            CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
