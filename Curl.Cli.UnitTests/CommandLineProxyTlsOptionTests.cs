using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NoProxyTlsOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
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
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-cert", certificate, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(certificate, result.Options.ProxyClientCertificate);
        Assert.IsNull(result.Options.ClientCertificate);
    }

    [TestMethod]
    [DataRow("--proxy-cert")]
    [DataRow("--proxy-key")]
    public void Parse_ProxyCertOrKeyGivenFlagLikeValue_WarnsAsAFileName(string option)
    {
        // curl --proxy-cert -zz file:///nosuch/zz warns, then exit 37 from the file URL (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = CommandLineParser.Parse([option, "-zz", Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([option, "-zz", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0, result.WarningLines.Count());
    }

    [TestMethod]
    public void Parse_EveryProxyTextOption_RecordsItsProxyValueAndLeavesTheOriginOnesUnset()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
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
        CommandLineParseResult result = CommandLineParser.Parse(
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
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--cert", "client.pem:a", "--proxy-cert", "proxy.pem:b", "--pass", "origin", "--proxy-pass", "proxy", Url],
            NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("client.pem:a", result.Options.ClientCertificate);
        Assert.AreEqual("proxy.pem:b", result.Options.ProxyClientCertificate);
        Assert.AreEqual("origin", result.Options.Passphrase);
        Assert.AreEqual("proxy", result.Options.ProxyPassphrase);
    }

    [TestMethod]
    public void Parse_ProxyCrlfileThatExists_ChecksAndRecordsIt()
    {
        string? checkedPath = null;

        CommandLineParseResult result = CommandLineParser.Parse(
            ["--proxy-crlfile", "proxy.crl", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("proxy.crl", result.Options.ProxyCertificateRevocationListFile);
        Assert.AreEqual("proxy.crl", checkedPath);
    }

    [TestMethod]
    public void Parse_ProxyCrlfileThatDoesNotExist_RefusesNamingProxyCrlfile()
    {
        // curl --proxy-crlfile nosuchfile.x file:///nosuch/zz -> exit 2 with these lines (curl 8.21.0, 2026-09-28).
        CommandLineParseResult result = CommandLineParser.Parse(["--proxy-crlfile", "nosuchfile.x", Url], NoPathExists);

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
        CommandLineParseResult result = CommandLineParser.Parse([option, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
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
        CommandLineParseResult result = CommandLineParser.Parse([option, "--no" + option[1..], Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
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
        CommandLineParseResult result = CommandLineParser.Parse([option, "x", Url], EveryPathExists);

        AssertRefused(result, $"curl: option {option}: the given option cannot be reversed with a --no- prefix");
    }

    private static bool ReadSwitch(CommandLineOptions options, string property) => property switch
    {
        nameof(CommandLineOptions.ProxyUseNativeCaStore) => options.ProxyUseNativeCaStore,
        nameof(CommandLineOptions.ProxyAutoClientCertificate) => options.ProxyAutoClientCertificate,
        _ => options.ProxyAllowBeast,
    };

    private static void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
