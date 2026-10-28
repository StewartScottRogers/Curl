using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Console;
using Curl.Networking;
using Curl.Testing;

namespace Curl.Engine;

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

    private TlsClientOptions? _expected;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromCommandLine_NoTlsOptions_VerifiesAgainstSystemStoreAtSystemDefaultVersion()
    {
        Assert.AreEqual(Expect(new TlsClientOptions()), Map(Url));
    }

    [TestMethod]
    public void FromCommandLine_Insecure_SetsInsecureOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(Insecure: true)), Map("-k", Url));
    }

    // CURLOPT_SSL_VERIFYPEER 0 with CURLOPT_SSL_VERIFYHOST on still checks the name (upstream test2118).
    [TestMethod]
    [DataRow(false, true, true)]
    [DataRow(false, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, false)]
    public void Of_VerifyPeerAndVerifyHost_ChecksHostNameWhenInsecureOnlyForHostWithoutPeer(bool verifyPeer, bool verifyHost, bool expected)
    {
        var peer = new TlsPeerOptions { VerifyPeer = verifyPeer, VerifyHost = verifyHost };
        var tls = new EasyTlsOptions { Origin = peer, Proxy = peer };

        Diagnostics.Arrange("verify peer, verify host", $"{verifyPeer}, {verifyHost}");

        var target = TlsClientOptionsMapping.Of(tls, useAlpn: true, sslSessionsFile: null);
        var proxy = TlsClientOptionsMapping.ProxyOf(tls, useAlpn: true);

        Diagnostics.Act("target, proxy", $"{target.ChecksHostNameWhenInsecure}, {proxy.ChecksHostNameWhenInsecure}");
        Assert.AreEqual(expected, target.ChecksHostNameWhenInsecure);
        Assert.AreEqual(expected, proxy.ChecksHostNameWhenInsecure);
        Assert.AreEqual(!verifyPeer, target.Insecure);
    }

    [TestMethod]
    public void OfProxyOfAndDohOf_CertificateAuthorityBlobs_CarryEachPeersBlob()
    {
        byte[] originBlob = [1, 2];
        byte[] proxyBlob = [3];
        var tls = new EasyTlsOptions
        {
            Origin = new TlsPeerOptions { CertificateAuthorityBlob = originBlob },
            Proxy = new TlsPeerOptions { CertificateAuthorityBlob = proxyBlob },
        };

        var target = TlsClientOptionsMapping.Of(tls, useAlpn: true, sslSessionsFile: null);
        var proxy = TlsClientOptionsMapping.ProxyOf(tls, useAlpn: true);
        var doh = TlsClientOptionsMapping.DohOf(tls);

        Diagnostics.Act("target, proxy, doh blob lengths", $"{target.CaCertificateBlob?.Length}, {proxy.CaCertificateBlob?.Length}, {doh.CaCertificateBlob?.Length}");
        CollectionAssert.AreEqual(originBlob, target.CaCertificateBlob!.Value.ToArray());
        CollectionAssert.AreEqual(proxyBlob, proxy.CaCertificateBlob!.Value.ToArray());
        CollectionAssert.AreEqual(originBlob, doh.CaCertificateBlob!.Value.ToArray());
    }

    [TestMethod]
    public void FromCommandLine_CaCertificateFile_CopiesThePathVerbatim()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(CaCertificateFile: "x.pem")), Map("--cacert", "x.pem", Url));
    }

    [TestMethod]
    public void FromCommandLine_CaCertificateDirectory_CopiesThePathVerbatim()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(CaCertificateDirectory: "certs")), Map("--capath", "certs", Url));
    }

    [TestMethod]
    public void FromCommandLine_ClientCertificate_CopiesThePathAndItsPassphraseAsSplit()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(ClientCertificate: "client.p12", Passphrase: "secret")), Map("--cert", "client.p12:secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_PrivateKey_CopiesThePathVerbatim()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(PrivateKey: "client.key")), Map("--key", "client.key", Url));
    }

    [TestMethod]
    public void FromCommandLine_CertType_CopiesItAsCertificateTypeVerbatim()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(CertificateType: "p12")), Map("--cert-type", "p12", Url));
    }

    [TestMethod]
    public void FromCommandLine_KeyType_CopiesItAsPrivateKeyTypeVerbatim()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(PrivateKeyType: "DER")), Map("--key-type", "DER", Url));
    }

    [TestMethod]
    public void FromCommandLine_Pass_CopiesItAsPassphraseVerbatim()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(Passphrase: "secret")), Map("--pass", "secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslNoRevoke_SetsSkipRevocationCheckOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(SkipRevocationCheck: true)), Map("--ssl-no-revoke", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslNoRevokeWithCaCertificateFile_SetsBoth()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(CaCertificateFile: "root.pem", SkipRevocationCheck: true)),
            Map("--ssl-no-revoke", "--cacert", "root.pem", Url));
    }

    [TestMethod]
    public void FromCommandLine_Ciphers_CopiesTheListVerbatim()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256:BOGUS")),
            Map("--ciphers", "ECDHE-RSA-AES128-GCM-SHA256:BOGUS", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tls13Ciphers_CopiesTheListVerbatim()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(Tls13Ciphers: "TLS_AES_128_GCM_SHA256")),
            Map("--tls13-ciphers", "TLS_AES_128_GCM_SHA256", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv12_SetsMinimumVersionTls12()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(MinimumVersion: TlsVersion.Tls12)), Map("--tlsv1.2", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv13_SetsMinimumVersionTls13()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(MinimumVersion: TlsVersion.Tls13)), Map("--tlsv1.3", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv13ThenTlsv12_LastOneWinsAsTls12()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(MinimumVersion: TlsVersion.Tls12)),
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
        Assert.AreEqual(Expect(new TlsClientOptions(MinimumVersion: expected)), Map(option, Url));
    }

    [TestMethod]
    [DataRow("1.0", TlsVersion.Tls10)]
    [DataRow("1.1", TlsVersion.Tls11)]
    [DataRow("1.2", TlsVersion.Tls12)]
    [DataRow("1.3", TlsVersion.Tls13)]
    [DataRow("default", TlsVersion.SystemDefault)]
    public void FromCommandLine_TlsMax_SetsMaximumVersionOnly(string version, TlsVersion expected)
    {
        Assert.AreEqual(Expect(new TlsClientOptions(MaximumVersion: expected)), Map("--tls-max", version, Url));
    }

    [TestMethod]
    public void FromCommandLine_MinimumAndTlsMax_SetsBothEnds()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(MinimumVersion: TlsVersion.Tls10, MaximumVersion: TlsVersion.Tls11)),
            Map("--tlsv1.0", "--tls-max", "1.1", Url));
    }

    [TestMethod]
    public void FromCommandLine_ProxyTlsv1_LeavesTheTargetsVersionsAlone()
    {
        Assert.AreEqual(Expect(new TlsClientOptions()), Map("--proxy-tlsv1", Url));
    }

    [TestMethod]
    public void FromCommandLine_NoSessionIdAndSslAllowBeast_SetNoSessionIdAndAllowBeast()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(NoSessionId: true)), Map("--no-sessionid", Url));
        Assert.AreEqual(Expect(new TlsClientOptions(AllowBeast: true)), Map("--ssl-allow-beast", Url));
        Assert.AreEqual(Expect(new TlsClientOptions()), Map("--proxy-ssl-allow-beast", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_NoSessionIdAndProxySslAllowBeast_SetNoSessionIdAndAllowBeast()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(NoSessionId: true)), MapProxy("--no-sessionid", Url));
        Assert.AreEqual(Expect(new TlsClientOptions(AllowBeast: true)), MapProxy("--proxy-ssl-allow-beast", Url));
        Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("--ssl-allow-beast", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyTlsv1_SetsTheProxysMinimumVersionTls10()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(MinimumVersion: TlsVersion.Tls10)), MapProxy("--proxy-tlsv1", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_TargetMinimumAndTlsMax_NeverReachTheProxy()
    {
        // curl 8.21.0 (Schannel) -x https://<a TLS 1.2-only proxy> --proxy-insecure with --tlsv1.3, with
        // --tls-max 1.1 and with --proxy-tlsv1 --tls-max 1.1 each completes the proxy handshake (BL-502).
        Assert.AreEqual(
            Expect(new TlsClientOptions(MinimumVersion: TlsVersion.Tls10)),
            MapProxy("--proxy-tlsv1", "--tlsv1.0", "--tls-max", "1.1", Url));
    }

    [TestMethod]
    [DataRow(SslVersionMaximum.None)]
    [DataRow(SslVersionMaximum.Default)]
    public void ToTlsVersion_CeilingNamingNoVersion_MapsToSystemDefault(SslVersionMaximum ceiling)
    {
        Diagnostics.Arrange("ceiling", ceiling);
        TlsVersion version = TlsClientOptionsMapping.ToTlsVersion(ceiling);
        Diagnostics.Act("TLS version", version);

        Diagnostics.Assert("TLS version", TlsVersion.SystemDefault, version);
        Assert.AreEqual(TlsVersion.SystemDefault, version);
    }

    [TestMethod]
    public void ToTlsVersion_SslVersionWithNoTlsVersion_MapsToSystemDefault()
    {
        Diagnostics.Arrange("SSL version", SslVersion.Ssl3);
        TlsVersion version = TlsClientOptionsMapping.ToTlsVersion(SslVersion.Ssl3);
        Diagnostics.Act("TLS version", version);

        Diagnostics.Assert("TLS version", TlsVersion.SystemDefault, version);
        Assert.AreEqual(
            TlsVersion.SystemDefault,
            TlsClientOptionsMapping.ToTlsVersion(SslVersion.Ssl3));
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists, then maps the result.
    /// </summary>
    [TestMethod]
    public void FromCommandLine_TheSevenTlsOptionsOfAdr0151StillInUse_CopiesEachVerbatim()
    {
        TlsClientOptions expected = new(
            Curves: "X25519",
            SignatureAlgorithms: "rsa_pss_rsae_sha256",
            AllowEarlyData: true,
            Ech: "hard",
            EchPublicName: "example.com",
            EchConfigList: "AEX+DQ==",
            SslSessionsFile: "sess.bin",
            Engine: "pkcs11");

        TlsClientOptions mapped = Map(
            "--curves", "X25519", "--sigalgs", "rsa_pss_rsae_sha256", "--tls-earlydata", "--ech", "hard",
            "--ech", "pn:example.com", "--ech", "ecl:AEX+DQ==", "--ssl-sessions", "sess.bin", "--engine", "pkcs11",
            Url);

        Diagnostics.Assert("target TLS options", expected, mapped);
        Assert.AreEqual(expected, mapped);
    }

    [TestMethod]
    public void ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions()),
            MapProxy("--curves", "X25519", "--sigalgs", "rsa_pss_rsae_sha256", "--tls-earlydata", "--ech", "hard",
                "--ssl-sessions", "sess.bin", "--engine", "pkcs11", "--dump-ca-embed",
                "--tlsuser", "user", "--tlspassword", "p", "--tlsauthtype", "SRP", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_TheProxyTlsSrpOptions_CarryNothingSinceCurl8220()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions()),
            MapProxy("--proxy-tlsuser", "proxyuser", "--proxy-tlspassword", "proxysecret", "--proxy-tlsauthtype", "SRP", Url));
    }

    [TestMethod]
    public void FromCommandLine_TheProxyTlsSrpOptions_NeverReachTheTarget()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions()),
            Map("--proxy-tlsuser", "proxyuser", "--proxy-tlspassword", "proxysecret", "--proxy-tlsauthtype", "SRP", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_TargetTlsOptionsOnly_VerifiesTheProxyAgainstTheSystemStore()
    {
        // curl -s -S -k -x https://localhost:18462 https://example.com/ and the same with
        // --cacert <the proxy's certificate> both fail with exit 60 (curl 8.21.0, 2026-09-27, BL-362).
        Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("-k", "--cacert", "x.pem", "--tlsv1.3", "--ssl-no-revoke", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyInsecureAndProxyCacert_SetsInsecureAndCaCertificateFile()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(Insecure: true, CaCertificateFile: "proxy.pem")),
            MapProxy("--proxy-insecure", "--proxy-cacert", "proxy.pem", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyCapathAndCapath_TakesProxyCapath()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(CaCertificateDirectory: "proxy-certs")),
            MapProxy("--capath", "certs", "--proxy-capath", "proxy-certs", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_CapathWithoutProxyCapath_FallsBackToCapath()
    {
        // curl --capath <dir> -x https://localhost:18462 https://example.com/ warns
        // "ignoring setting the CA path for the proxy" as --proxy-capath does (curl 8.21.0, 2026-09-27).
        Assert.AreEqual(Expect(new TlsClientOptions(CaCertificateDirectory: "certs")), MapProxy("--capath", "certs", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslRevokeBestEffort_SetsRevocationCheckBestEffortOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(RevocationCheckBestEffort: true)), Map("--ssl-revoke-best-effort", Url));
    }

    [TestMethod]
    public void FromCommandLine_NoAlpn_TurnsAlpnOff()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(UseAlpn: false)), Map("--no-alpn", Url));
    }

    [TestMethod]
    public void FromCommandLine_NoAlpnThenAlpn_LeavesAlpnOn()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(UseAlpn: true)), Map("--no-alpn", "--alpn", Url));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FromCommandLine_CaNative_ChangesNothingOnEitherBuild(bool withCaCertificateFile)
    {
        string[] trustArguments = withCaCertificateFile ? ["--cacert", "root.pem"] : [];
        // Measured (BL-490): curl 8.21.0 Schannel and curl 8.18.0 OpenSSL on Ubuntu give the
        // same answer with and without --ca-native, with and without --cacert (ADR-0124).
        TlsClientOptions without = Map([.. trustArguments, Url]);
        TlsClientOptions with = Map([.. trustArguments, "--ca-native", Url]);
        Diagnostics.Assert("target TLS options with --ca-native", without, with);
        Assert.AreEqual(
            Map([.. trustArguments, Url]),
            Map([.. trustArguments, "--ca-native", Url]));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void FromCommandLine_CertStatusOnOpenSslBuild_SetsRequireCertificateStatusOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(RequireCertificateStatus: true)), Map("--cert-status", Url));
    }

    // curl 8.22.0's Schannel build ignores --cert-status: a server stapling no OCSP response
    // answers exit 0 with its body (measured, ADR-0795).
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FromCommandLine_CertStatusOnSchannelBuild_IsIgnored()
    {
        Assert.AreEqual(Expect(new TlsClientOptions()), Map("--cert-status", Url));
    }

    [TestMethod]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    public void RequiresCertificateStatus_VerifyStatusAndBuild_RequiresItOnlyOutsideSchannel(bool verifyStatus, bool matchesSchannelBuild, bool expected)
    {
        Diagnostics.Arrange("verify status, Schannel build", $"{verifyStatus}, {matchesSchannelBuild}");

        bool requires = TlsClientOptionsMapping.RequiresCertificateStatus(verifyStatus, matchesSchannelBuild);

        Diagnostics.Act("requires certificate status", $"{requires}");
        Assert.AreEqual(expected, requires);
    }

    [TestMethod]
    public void FromCommandLine_SslAutoClientCert_SetsAutoClientCertificateOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(AutoClientCertificate: true)), Map("--ssl-auto-client-cert", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxySslAutoClientCert_SetsTheProxysAutoClientCertificateOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(AutoClientCertificate: true)), MapProxy("--proxy-ssl-auto-client-cert", Url));
        Assert.AreEqual(Expect(new TlsClientOptions(AutoClientCertificate: true)), Map("--proxy-ssl-auto-client-cert", "--ssl-auto-client-cert", Url));
    }

    [TestMethod]
    public void FromCommandLine_Pinnedpubkey_SetsTheTargetsPinnedPublicKeyOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(PinnedPublicKey: "sha256//a=;sha256//b=")), Map("--pinnedpubkey", "sha256//a=;sha256//b=", Url));
        Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("--pinnedpubkey", "sha256//a=", Url));
    }

    [TestMethod]
    public void FromCommandLine_Crlfile_SetsTheTargetsCertificateRevocationListFileOnly()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(CertificateRevocationListFile: "revoked.crl")), Map("--crlfile", "revoked.crl", Url));
        Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("--crlfile", "revoked.crl", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyClientCertificateOptions_ReachTheProxyWithTheCertificateSplitFromItsPassphrase()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(
                ClientCertificate: "proxy:name.pem",
                PrivateKey: "proxy-key.pem",
                CertificateType: "P12",
                PrivateKeyType: "DER",
                Passphrase: "secret")),
            MapProxy(
                "--proxy-cert", @"proxy\:name.pem:se\:cret", "--proxy-key", "proxy-key.pem", "--proxy-cert-type", "P12",
                "--proxy-key-type", "DER", "--proxy-pass", "secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_ProxyClientCertificateOptions_NeverReachTheTarget()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions()),
            Map("--proxy-cert", "proxy.pem", "--proxy-key", "proxy-key.pem", "--proxy-cert-type", "P12",
                "--proxy-key-type", "DER", "--proxy-pass", "secret", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyCipherLists_ReachTheProxyVerbatim()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256", Tls13Ciphers: "TLS_AES_128_GCM_SHA256")),
            MapProxy("--proxy-ciphers", "ECDHE-RSA-AES128-GCM-SHA256", "--proxy-tls13-ciphers", "TLS_AES_128_GCM_SHA256", Url));
    }

    [TestMethod]
    public void FromCommandLine_ProxyCipherLists_NeverReachTheTarget()
    {
        Assert.AreEqual(Expect(new TlsClientOptions()), Map("--proxy-ciphers", "BOGUS", "--proxy-tls13-ciphers", "BOGUS", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_TargetClientCertificateAndCipherOptions_NeverReachTheProxy()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions()),
            MapProxy("--cert", "client.pem", "--key", "key.pem", "--cert-type", "P12", "--key-type", "DER", "--pass", "secret",
                "--ciphers", "BOGUS", "--tls13-ciphers", "BOGUS", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_CertStatusAndSslAutoClientCert_NeverReachTheProxy()
    {
        Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("--cert-status", "--ssl-auto-client-cert", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyPinnedpubkeyAndProxyCrlfile_ReachTheProxyVerbatim()
    {
        Assert.AreEqual(
            Expect(new TlsClientOptions(PinnedPublicKey: "sha256//a=;sha256//b=", CertificateRevocationListFile: "proxy.crl")),
            MapProxy("--proxy-pinnedpubkey", "sha256//a=;sha256//b=", "--proxy-crlfile", "proxy.crl", Url));
        Assert.AreEqual(Expect(new TlsClientOptions()), Map("--proxy-pinnedpubkey", "sha256//a=", "--proxy-crlfile", "proxy.crl", Url));
    }

    [TestMethod]
    public void ProxyFromCommandLine_NoAlpn_TurnsTheProxysAlpnOff()
    {
        // curl -v --no-alpn --proxy-insecure -x https://... http://... prints no ALPN line for the
        // proxy on both builds (curl 8.21.0, measured, BL-753).
        Assert.AreEqual(Expect(new TlsClientOptions(UseAlpn: false)), MapProxy("--no-alpn", Url));
        Diagnostics.Assert("proxy ALPN without --no-alpn", true, MapProxy(Url).UseAlpn);
        Assert.IsTrue(MapProxy(Url).UseAlpn);
    }

    [TestMethod]
    public void ProxyFromCommandLine_ProxyCaNativeChangesNothingAndProxySslAllowBeastOnlyTheProxysSplit()
    {
        Assert.AreEqual(Expect(new TlsClientOptions(AllowBeast: true)), MapProxy("--proxy-ca-native", "--proxy-ssl-allow-beast", Url));
        Assert.AreEqual(Expect(new TlsClientOptions()), Map("--proxy-ca-native", "--proxy-ssl-allow-beast", Url));
    }

    private TlsClientOptions Map(params string[] arguments) =>
        Mapped("target", arguments, MapTarget(Parse(arguments)));

    private TlsClientOptions MapProxy(params string[] arguments) =>
        Mapped("proxy", arguments, MapProxyOf(Parse(arguments)));

    // The target's options through the whole command line, as the engine maps them from the TLS record.
    private static TlsClientOptions MapTarget(CommandLineOptions options) =>
        TlsClientOptionsMapping.Of(EasyOptionsFromCommandLine.TlsOf(options), options.UseAlpn, options.SslSessionsFile);

    // The HTTPS proxy's options through the whole command line, as the engine maps them from the TLS record.
    private static TlsClientOptions MapProxyOf(CommandLineOptions options) =>
        TlsClientOptionsMapping.ProxyOf(EasyOptionsFromCommandLine.TlsOf(options), options.UseAlpn);

    /// <summary>Notes the options the next <see cref="Mapped" /> is expected to give, and returns them.</summary>
    private TlsClientOptions Expect(TlsClientOptions expected)
    {
        _expected = expected;
        return expected;
    }

    /// <summary>Writes the command line and the options it mapped to, against the expected options if any were noted.</summary>
    private TlsClientOptions Mapped(string side, string[] arguments, TlsClientOptions mapped)
    {
        Diagnostics.Arrange("command line", string.Join(' ', arguments));
        Diagnostics.Act(side + " TLS options", mapped);
        if (_expected is not null)
        {
            Diagnostics.Assert(side + " TLS options", _expected, mapped);
            _expected = null;
        }

        return mapped;
    }

    private static CommandLineOptions Parse(string[] arguments)
    {
        // As the OpenSSL build reads it, but built with ECH, so --ech is accepted rather than refused (ADR-0918).
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForProcess, new DefaultConfigFileSearch(_ => null, isWindows: false, null, null), isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    // BL-2327: the OpenSSL build logs to SSLKEYLOGFILE; the Schannel build ignores it (test2090 needs !Schannel).
    [TestMethod]
    [DataRow("/tmp/2090.log.ssl", false, "/tmp/2090.log.ssl")]
    [DataRow("/tmp/2090.log.ssl", true, null)]
    [DataRow("", false, null)]
    [DataRow(null, false, null)]
    public void KeyLogFileFromEnvironment_ReadsSslKeyLogFileOnlyOutsideTheSchannelBuild(string? value, bool matchesSchannelBuild, string? expected)
    {
        string? askedFor = null;
        Diagnostics.Arrange("SSLKEYLOGFILE", value);
        Diagnostics.Arrange("Schannel build", matchesSchannelBuild);

        string? actual = TlsClientOptionsMapping.KeyLogFileFromEnvironment(name => { askedFor = name; return value; }, matchesSchannelBuild);
        Diagnostics.Act("key log file", actual);

        Diagnostics.Assert("key log file", expected, actual);
        Assert.AreEqual(expected, actual);
        Assert.AreEqual(matchesSchannelBuild ? null : "SSLKEYLOGFILE", askedFor);
    }

    // BL-2716: the in-process run gives its case's <setenv> as its own environment, so the target's and the
    // HTTPS proxy's key-log file come from the reader the run was given, not the process's environment.
    [TestMethod]
    public void Of_GivenAnEnvironmentReader_TakesTheKeyLogFileFromIt()
    {
        TlsClientOptions mapped = Mapped("target", [Url], TlsClientOptionsMapping.Of(EasyOptionsFromCommandLine.TlsOf(Parse([Url])), useAlpn: true, sslSessionsFile: null, KeyLogEnvironment));

        Diagnostics.Assert("key log file", KeyLogFileOffWindows, mapped.KeyLogFile);
        Assert.AreEqual(KeyLogFileOffWindows, mapped.KeyLogFile);
    }

    [TestMethod]
    public void ProxyOf_GivenAnEnvironmentReader_TakesTheKeyLogFileFromIt()
    {
        TlsClientOptions mapped = Mapped("proxy", [Url], TlsClientOptionsMapping.ProxyOf(EasyOptionsFromCommandLine.TlsOf(Parse([Url])), useAlpn: true, KeyLogEnvironment));

        Diagnostics.Assert("key log file", KeyLogFileOffWindows, mapped.KeyLogFile);
        Assert.AreEqual(KeyLogFileOffWindows, mapped.KeyLogFile);
    }

    private const string KeyLogPath = "log/2090.log.ssl";

    // The Schannel build Curl matches on Windows ignores SSLKEYLOGFILE; the OpenSSL build elsewhere reads it.
    private static string? KeyLogFileOffWindows => OperatingSystem.IsWindows() ? null : KeyLogPath;

    private static string? KeyLogEnvironment(string name) => name == "SSLKEYLOGFILE" ? KeyLogPath : null;
}
