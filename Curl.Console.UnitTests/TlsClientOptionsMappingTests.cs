using System.Security.Authentication;
using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins how <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, <c>--cert</c>, <c>--key</c>,
/// <c>--ciphers</c>, <c>--tls13-ciphers</c>, <c>--cert-type</c>, <c>--key-type</c>, <c>--pass</c>, <c>--ssl-no-revoke</c>, <c>--tlsv1.2</c> and <c>--tlsv1.3</c> become the
/// <see cref="TlsClientOptions" /> the TLS provider applies. No test here opens a socket or
/// reads a certificate file.
/// </summary>
[TestClass]
public sealed class TlsClientOptionsMappingTests
{
    private const string Url = "gophers://example.com/";

    [TestMethod]
    public void FromCommandLine_NoTlsOptions_VerifiesAgainstSystemStoreAtSystemDefaultVersion()
    {
        Assert.AreEqual(new TlsClientOptions(), Map(Url));
    }

    [TestMethod]
    public void FromCommandLine_Insecure_SetsInsecureOnly()
    {
        Assert.AreEqual(new TlsClientOptions(Insecure: true), Map("-k", Url));
    }

    [TestMethod]
    public void FromCommandLine_CaCertificateFile_CopiesThePathVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(CaCertificateFile: "x.pem"), Map("--cacert", "x.pem", Url));
    }

    [TestMethod]
    public void FromCommandLine_CaCertificateDirectory_CopiesThePathVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(CaCertificateDirectory: "certs"), Map("--capath", "certs", Url));
    }

    [TestMethod]
    public void FromCommandLine_ClientCertificate_CopiesTheValueWithItsPassphraseVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(ClientCertificate: "client.p12:secret"), Map("--cert", "client.p12:secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_PrivateKey_CopiesThePathVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(PrivateKey: "client.key"), Map("--key", "client.key", Url));
    }

    [TestMethod]
    public void FromCommandLine_CertType_CopiesItAsCertificateTypeVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(CertificateType: "p12"), Map("--cert-type", "p12", Url));
    }

    [TestMethod]
    public void FromCommandLine_KeyType_CopiesItAsPrivateKeyTypeVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(PrivateKeyType: "DER"), Map("--key-type", "DER", Url));
    }

    [TestMethod]
    public void FromCommandLine_Pass_CopiesItAsPassphraseVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(Passphrase: "secret"), Map("--pass", "secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslNoRevoke_SetsSkipRevocationCheckOnly()
    {
        Assert.AreEqual(new TlsClientOptions(SkipRevocationCheck: true), Map("--ssl-no-revoke", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslNoRevokeWithCaCertificateFile_SetsBoth()
    {
        Assert.AreEqual(
            new TlsClientOptions(CaCertificateFile: "root.pem", SkipRevocationCheck: true),
            Map("--ssl-no-revoke", "--cacert", "root.pem", Url));
    }

    [TestMethod]
    public void FromCommandLine_Ciphers_CopiesTheListVerbatim()
    {
        Assert.AreEqual(
            new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256:BOGUS"),
            Map("--ciphers", "ECDHE-RSA-AES128-GCM-SHA256:BOGUS", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tls13Ciphers_CopiesTheListVerbatim()
    {
        Assert.AreEqual(
            new TlsClientOptions(Tls13Ciphers: "TLS_AES_128_GCM_SHA256"),
            Map("--tls13-ciphers", "TLS_AES_128_GCM_SHA256", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv12_SetsMinimumVersionTls12()
    {
        Assert.AreEqual(new TlsClientOptions(MinimumVersion: TlsVersion.Tls12), Map("--tlsv1.2", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv13_SetsMinimumVersionTls13()
    {
        Assert.AreEqual(new TlsClientOptions(MinimumVersion: TlsVersion.Tls13), Map("--tlsv1.3", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv13ThenTlsv12_LastOneWinsAsTls12()
    {
        Assert.AreEqual(
            new TlsClientOptions(MinimumVersion: TlsVersion.Tls12),
            Map("--tlsv1.3", "--tlsv1.2", Url));
    }

    [TestMethod]
    [DataRow("-1", TlsVersion.Tls10)]
    [DataRow("--tlsv1", TlsVersion.Tls10)]
    [DataRow("--tlsv1.0", TlsVersion.Tls10)]
    [DataRow("--tlsv1.1", TlsVersion.Tls11)]
    [DataRow("--tlsv1.2", TlsVersion.Tls12)]
    [DataRow("--tlsv1.3", TlsVersion.Tls13)]
    public void FromCommandLine_MinimumVersionOption_SetsMinimumVersionOnly(string option, TlsVersion expected)
    {
        Assert.AreEqual(new TlsClientOptions(MinimumVersion: expected), Map(option, Url));
    }

    [TestMethod]
    [DataRow("1.0", TlsVersion.Tls10)]
    [DataRow("1.1", TlsVersion.Tls11)]
    [DataRow("1.2", TlsVersion.Tls12)]
    [DataRow("1.3", TlsVersion.Tls13)]
    [DataRow("default", TlsVersion.SystemDefault)]
    public void FromCommandLine_TlsMax_SetsMaximumVersionOnly(string version, TlsVersion expected)
    {
        Assert.AreEqual(new TlsClientOptions(MaximumVersion: expected), Map("--tls-max", version, Url));
    }

    [TestMethod]
    public void FromCommandLine_MinimumAndTlsMax_SetsBothEnds()
    {
        Assert.AreEqual(
            new TlsClientOptions(MinimumVersion: TlsVersion.Tls10, MaximumVersion: TlsVersion.Tls11),
            Map("--tlsv1.0", "--tls-max", "1.1", Url));
    }

    [TestMethod]
    public void FromCommandLine_ProxyTlsv1_LeavesTheTargetsVersionsAlone()
    {
        Assert.AreEqual(new TlsClientOptions(), Map("--proxy-tlsv1", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyTlsv1_SetsTheProxysMinimumVersionTls10()
    {
        Assert.AreEqual(new TlsClientOptions(MinimumVersion: TlsVersion.Tls10), MapProxy("--proxy-tlsv1", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_TargetMinimumAndTlsMax_NeverReachTheProxy()
    {
        // curl 8.21.0 (Schannel) -x https://<a TLS 1.2-only proxy> --proxy-insecure with --tlsv1.3, with
        // --tls-max 1.1 and with --proxy-tlsv1 --tls-max 1.1 each completes the proxy handshake (BL-502).
        Assert.AreEqual(
            new TlsClientOptions(MinimumVersion: TlsVersion.Tls10),
            MapProxy("--proxy-tlsv1", "--tlsv1.0", "--tls-max", "1.1", Url));
    }

    [TestMethod]
    public void ToTlsVersion_OtherNonNullVersion_MapsToSystemDefault()
    {
        Assert.AreEqual(
            TlsVersion.SystemDefault,
            TlsClientOptionsMapping.ToTlsVersion(SslProtocols.None));
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists, then maps the result.
    /// </summary>
    [TestMethod]
    public void FromCommandLine_TheTenTlsOptionsOfAdr0151_CopiesEachVerbatim()
    {
        TlsClientOptions expected = new(
            Curves: "X25519",
            SignatureAlgorithms: "rsa_pss_rsae_sha256",
            AllowEarlyData: true,
            Ech: "hard",
            EchPublicName: "example.com",
            EchConfigList: "AEX+DQ==",
            SslSessionsFile: "sess.bin",
            Engine: "pkcs11",
            TlsUser: "user",
            TlsPassword: "secret",
            TlsAuthType: "SRP");

        TlsClientOptions mapped = Map(
            "--curves", "X25519", "--sigalgs", "rsa_pss_rsae_sha256", "--tls-earlydata", "--ech", "hard",
            "--ech", "pn:example.com", "--ech", "ecl:AEX+DQ==", "--ssl-sessions", "sess.bin", "--engine", "pkcs11",
            "--tlsuser", "user", "--tlspassword", "secret", "--tlsauthtype", "SRP", Url);

        Assert.AreEqual(expected, mapped);
    }

    [TestMethod]
    public void ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy()
    {
        Assert.AreEqual(
            new TlsClientOptions(),
            MapProxy("--curves", "X25519", "--tls-earlydata", "--ech", "true", "--tlsuser", "user", "--tlspassword", "p", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_TargetTlsOptionsOnly_VerifiesTheProxyAgainstTheSystemStore()
    {
        // curl -s -S -k -x https://localhost:18462 https://example.com/ and the same with
        // --cacert <the proxy's certificate> both fail with exit 60 (curl 8.21.0, 2026-09-27, BL-362).
        Assert.AreEqual(new TlsClientOptions(), MapProxy("-k", "--cacert", "x.pem", "--tlsv1.3", "--ssl-no-revoke", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyInsecureAndProxyCacert_SetsInsecureAndCaCertificateFile()
    {
        Assert.AreEqual(
            new TlsClientOptions(Insecure: true, CaCertificateFile: "proxy.pem"),
            MapProxy("--proxy-insecure", "--proxy-cacert", "proxy.pem", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyCapathAndCapath_TakesProxyCapath()
    {
        Assert.AreEqual(
            new TlsClientOptions(CaCertificateDirectory: "proxy-certs"),
            MapProxy("--capath", "certs", "--proxy-capath", "proxy-certs", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_CapathWithoutProxyCapath_FallsBackToCapath()
    {
        // curl --capath <dir> -x https://localhost:18462 https://example.com/ warns
        // "ignoring setting the CA path for the proxy" as --proxy-capath does (curl 8.21.0, 2026-09-27).
        Assert.AreEqual(new TlsClientOptions(CaCertificateDirectory: "certs"), MapProxy("--capath", "certs", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslRevokeBestEffort_SetsRevocationCheckBestEffortOnly()
    {
        Assert.AreEqual(new TlsClientOptions(RevocationCheckBestEffort: true), Map("--ssl-revoke-best-effort", Url));
    }

    [TestMethod]
    public void FromCommandLine_NoAlpn_TurnsAlpnOff()
    {
        Assert.AreEqual(new TlsClientOptions(UseAlpn: false), Map("--no-alpn", Url));
    }

    [TestMethod]
    public void FromCommandLine_NoAlpnThenAlpn_LeavesAlpnOn()
    {
        Assert.AreEqual(new TlsClientOptions(UseAlpn: true), Map("--no-alpn", "--alpn", Url));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FromCommandLine_CaNative_ChangesNothingOnEitherBuild(bool withCaCertificateFile)
    {
        string[] trustArguments = withCaCertificateFile ? ["--cacert", "root.pem"] : [];
        // Measured (BL-490): curl 8.21.0 Schannel and curl 8.18.0 OpenSSL on Ubuntu give the
        // same answer with and without --ca-native, with and without --cacert (ADR-0124).
        Assert.AreEqual(
            Map([.. trustArguments, Url]),
            Map([.. trustArguments, "--ca-native", Url]));
    }

    [TestMethod]
    public void FromCommandLine_CertStatus_SetsRequireCertificateStatusOnly()
    {
        Assert.AreEqual(new TlsClientOptions(RequireCertificateStatus: true), Map("--cert-status", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslAutoClientCert_SetsAutoClientCertificateOnly()
    {
        Assert.AreEqual(new TlsClientOptions(AutoClientCertificate: true), Map("--ssl-auto-client-cert", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxySslAutoClientCert_SetsTheProxysAutoClientCertificateOnly()
    {
        Assert.AreEqual(new TlsClientOptions(AutoClientCertificate: true), MapProxy("--proxy-ssl-auto-client-cert", Url));
        Assert.AreEqual(new TlsClientOptions(AutoClientCertificate: true), Map("--proxy-ssl-auto-client-cert", "--ssl-auto-client-cert", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_CertStatusAndSslAutoClientCert_NeverReachTheProxy()
    {
        Assert.AreEqual(new TlsClientOptions(), MapProxy("--cert-status", "--ssl-auto-client-cert", Url));
    }

    private static TlsClientOptions Map(params string[] arguments) =>
        TlsClientOptionsMapping.FromCommandLine(Parse(arguments));

    private static TlsClientOptions MapProxy(params string[] arguments) =>
        TlsClientOptionsMapping.ProxyFromCommandLine(Parse(arguments));

    private static CommandLineOptions Parse(string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
