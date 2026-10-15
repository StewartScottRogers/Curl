using Curl.Api.Options.Tls;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.TlsOf" />: each TLS command-line option given sets its
/// <see cref="EasyTlsOptions" /> member, and a command line without it keeps libcurl's default (ADR-0514).
/// </summary>
public sealed partial class EasyOptionsFromCommandLineTests
{
    private static readonly EasyTlsOptions LibcurlTlsDefaults = new();

    public static IEnumerable<object?[]> GivenTlsOptions =>
    [
        GivenTls("-k peer", o => o.Origin.VerifyPeer, false, "-k"),
        GivenTls("-k host", o => o.Origin.VerifyHost, false, "-k"),
        GivenTls("-k leaves the proxy", o => o.Proxy.VerifyPeer, true, "-k"),
        GivenTls("--cacert", o => o.Origin.CertificateAuthorityFile, "ca.pem", "--cacert", "ca.pem"),
        GivenTls("--capath", o => o.Origin.CertificateAuthorityPath, "certs", "--capath", "certs"),
        GivenTls("--capath reaches the proxy", o => o.Proxy.CertificateAuthorityPath, "certs", "--capath", "certs"),
        GivenTls("--cert", o => o.Origin.Certificate, "client.pem", "--cert", "client.pem"),
        GivenTls("--cert passphrase", o => o.Origin.KeyPassword, "secret", "--cert", "client.pem:secret"),
        GivenTls("--cert name before passphrase", o => o.Origin.Certificate, "client.pem", "--cert", "client.pem:secret"),
        GivenTls("--pass outranks --cert's", o => o.Origin.KeyPassword, "phrase", "--cert", "client.pem:secret", "--pass", "phrase"),
        GivenTls("--pass", o => o.Origin.KeyPassword, "phrase", "--pass", "phrase"),
        GivenTls("--cert pkcs11 unsplit", o => o.Origin.Certificate, "pkcs11:token=a;object=b", "--cert", "pkcs11:token=a;object=b"),
        GivenTls("--cert pkcs11 type left unset", o => o.Origin.CertificateType, null, "--cert", "pkcs11:token=a;object=b"),
        GivenTls("--cert-type", o => o.Origin.CertificateType, "P12", "--cert-type", "P12"),
        GivenTls("--key", o => o.Origin.Key, "key.pem", "--key", "key.pem"),
        GivenTls("--key pkcs11 type left unset", o => o.Origin.KeyType, null, "--key", "pkcs11:object=k"),
        GivenTls("--key-type", o => o.Origin.KeyType, "DER", "--key-type", "DER"),
        GivenTls("--ciphers", o => o.Origin.CipherList, "ECDHE-RSA-AES128-GCM-SHA256", "--ciphers", "ECDHE-RSA-AES128-GCM-SHA256"),
        GivenTls("--tls13-ciphers", o => o.Origin.Tls13Ciphers, "TLS_AES_128_GCM_SHA256", "--tls13-ciphers", "TLS_AES_128_GCM_SHA256"),
        GivenTls("--tlsv1", o => o.Origin.SslVersion, SslVersion.Tls10, "--tlsv1"),
        GivenTls("--tlsv1.0", o => o.Origin.SslVersion, SslVersion.Tls10, "--tlsv1.0"),
        GivenTls("--tlsv1.1", o => o.Origin.SslVersion, SslVersion.Tls11, "--tlsv1.1"),
        GivenTls("--tlsv1.2", o => o.Origin.SslVersion, SslVersion.Tls12, "--tlsv1.2"),
        GivenTls("--tlsv1.3", o => o.Origin.SslVersion, SslVersion.Tls13, "--tlsv1.3"),
        GivenTls("--tls-max 1.0", o => o.Origin.SslVersionMaximum, SslVersionMaximum.Tls10, "--tls-max", "1.0"),
        GivenTls("--tls-max 1.1", o => o.Origin.SslVersionMaximum, SslVersionMaximum.Tls11, "--tls-max", "1.1"),
        GivenTls("--tls-max 1.2", o => o.Origin.SslVersionMaximum, SslVersionMaximum.Tls12, "--tls-max", "1.2"),
        GivenTls("--tls-max 1.3", o => o.Origin.SslVersionMaximum, SslVersionMaximum.Tls13, "--tls-max", "1.3"),
        GivenTls("--tls-max default", o => o.Origin.SslVersionMaximum, SslVersionMaximum.None, "--tls-max", "default"),
        GivenTls("--tls-max leaves the proxy", o => o.Proxy.SslVersionMaximum, SslVersionMaximum.None, "--tls-max", "1.2"),
        GivenTls("--crlfile", o => o.Origin.RevocationListFile, "revoked.pem", "--crlfile", "revoked.pem"),
        GivenTls("--pinnedpubkey", o => o.Origin.PinnedPublicKey, "sha256//abc=", "--pinnedpubkey", "sha256//abc="),
        GivenTls("--ssl-allow-beast", o => o.Origin.SslOptions, SslOptions.AllowBeast, "--ssl-allow-beast"),
        GivenTls("--ssl-no-revoke", o => o.Origin.SslOptions, SslOptions.NoRevoke, "--ssl-no-revoke"),
        GivenTls("--ssl-revoke-best-effort", o => o.Origin.SslOptions, SslOptions.RevokeBestEffort, "--ssl-revoke-best-effort"),
        GivenTls("--ca-native", o => o.Origin.SslOptions, SslOptions.NativeCa, "--ca-native"),
        GivenTls("--ssl-auto-client-cert", o => o.Origin.SslOptions, SslOptions.AutoClientCert, "--ssl-auto-client-cert"),
        GivenTls("--tls-earlydata", o => o.Origin.SslOptions, SslOptions.EarlyData, "--tls-earlydata"),
        GivenTls("ssl options combine", o => o.Origin.SslOptions, SslOptions.AllowBeast | SslOptions.NoRevoke, "--ssl-allow-beast", "--ssl-no-revoke"),
        GivenTls("--proxy-insecure peer", o => o.Proxy.VerifyPeer, false, "--proxy-insecure"),
        GivenTls("--proxy-insecure host", o => o.Proxy.VerifyHost, false, "--proxy-insecure"),
        GivenTls("--proxy-insecure leaves the origin", o => o.Origin.VerifyPeer, true, "--proxy-insecure"),
        GivenTls("--proxy-cacert", o => o.Proxy.CertificateAuthorityFile, "proxy-ca.pem", "--proxy-cacert", "proxy-ca.pem"),
        GivenTls("--cacert leaves the proxy", o => o.Proxy.CertificateAuthorityFile, null, "--cacert", "ca.pem"),
        GivenTls("--proxy-capath", o => o.Proxy.CertificateAuthorityPath, "proxy-certs", "--capath", "certs", "--proxy-capath", "proxy-certs"),
        GivenTls("--proxy-cert", o => o.Proxy.Certificate, "proxy.pem", "--proxy-cert", "proxy.pem:secret"),
        GivenTls("--proxy-cert passphrase", o => o.Proxy.KeyPassword, "secret", "--proxy-cert", "proxy.pem:secret"),
        GivenTls("--proxy-pass", o => o.Proxy.KeyPassword, "phrase", "--proxy-cert", "proxy.pem:secret", "--proxy-pass", "phrase"),
        GivenTls("--proxy-cert-type", o => o.Proxy.CertificateType, "DER", "--proxy-cert-type", "DER"),
        GivenTls("--proxy-key", o => o.Proxy.Key, "proxy-key.pem", "--proxy-key", "proxy-key.pem"),
        GivenTls("--proxy-key-type", o => o.Proxy.KeyType, "DER", "--proxy-key-type", "DER"),
        GivenTls("--proxy-ciphers", o => o.Proxy.CipherList, "AES128-SHA", "--proxy-ciphers", "AES128-SHA"),
        GivenTls("--proxy-tls13-ciphers", o => o.Proxy.Tls13Ciphers, "TLS_AES_256_GCM_SHA384", "--proxy-tls13-ciphers", "TLS_AES_256_GCM_SHA384"),
        GivenTls("--proxy-crlfile", o => o.Proxy.RevocationListFile, "proxy-revoked.pem", "--proxy-crlfile", "proxy-revoked.pem"),
        GivenTls("--proxy-pinnedpubkey", o => o.Proxy.PinnedPublicKey, "sha256//def=", "--proxy-pinnedpubkey", "sha256//def="),
        GivenTls("--proxy-tlsv1", o => o.Proxy.SslVersion, SslVersion.Tls1, "--proxy-tlsv1"),
        GivenTls("--proxy-ssl-allow-beast", o => o.Proxy.SslOptions, SslOptions.AllowBeast, "--proxy-ssl-allow-beast"),
        GivenTls("--proxy-ca-native", o => o.Proxy.SslOptions, SslOptions.NativeCa, "--proxy-ca-native"),
        GivenTls("--proxy-ssl-auto-client-cert", o => o.Proxy.SslOptions, SslOptions.AutoClientCert, "--proxy-ssl-auto-client-cert"),
        GivenTls("--engine", o => o.Engine, "pkcs11", "--engine", "pkcs11"),
        GivenTls("--cert-status", o => o.VerifyStatus, true, "--cert-status"),
        GivenTls("-w", o => o.GatherCertificateInfo, true, "-w", "%{certs}"),
        GivenTls("--curves", o => o.EllipticCurves, "X25519", "--curves", "X25519"),
        GivenTls("--sigalgs", o => o.SignatureAlgorithms, "ECDSA+SHA256", "--sigalgs", "ECDSA+SHA256"),
        GivenTls("--no-sessionid", o => o.SessionIdCache, false, "--no-sessionid"),
        GivenTls("--ech mode", o => o.EncryptedClientHello, "grease", "--ech", "grease"),
        GivenTls("--ech pn:", o => o.EncryptedClientHelloPublicName, "public.example", "--ech", "pn:public.example"),
        GivenTls("--ech ecl:", o => o.EncryptedClientHelloConfigList, "AEX+DQBB", "--ech", "ecl:AEX+DQBB"),
        GivenTls("--doh-insecure peer", o => o.DohVerifyPeer, false, "--doh-insecure"),
        GivenTls("--doh-insecure host", o => o.DohVerifyHost, false, "--doh-insecure"),
        GivenTls("--doh-cert-status", o => o.DohVerifyStatus, true, "--doh-cert-status"),
        GivenTls("--false-start", o => o.FalseStart, false, "--false-start"),
    ];

    public static IEnumerable<object?[]> AbsentTlsOptions =>
    [
        AbsentTls("Origin", o => o.Origin),
        AbsentTls("Proxy", o => o.Proxy),
        AbsentTls("Engine", o => o.Engine),
        AbsentTls("EngineIsDefault", o => o.EngineIsDefault),
        AbsentTls("VerifyStatus", o => o.VerifyStatus),
        AbsentTls("CertificateAuthorityCacheTimeout", o => o.CertificateAuthorityCacheTimeout),
        AbsentTls("GatherCertificateInfo", o => o.GatherCertificateInfo),
        AbsentTls("EllipticCurves", o => o.EllipticCurves),
        AbsentTls("SignatureAlgorithms", o => o.SignatureAlgorithms),
        AbsentTls("SessionIdCache", o => o.SessionIdCache),
        AbsentTls("FalseStart", o => o.FalseStart),
        AbsentTls("EncryptedClientHello", o => o.EncryptedClientHello),
        AbsentTls("EncryptedClientHelloPublicName", o => o.EncryptedClientHelloPublicName),
        AbsentTls("EncryptedClientHelloConfigList", o => o.EncryptedClientHelloConfigList),
        AbsentTls("DohVerifyPeer", o => o.DohVerifyPeer),
        AbsentTls("DohVerifyHost", o => o.DohVerifyHost),
        AbsentTls("DohVerifyStatus", o => o.DohVerifyStatus),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenTlsOptions))]
    public void TlsOf_OptionGiven_SetsItsMember(string option, Func<EasyTlsOptions, object?> read, object? expected, string[] arguments)
    {
        EasyTlsOptions tls = EasyOptionsFromCommandLine.TlsOf(Parse(arguments));

        Assert.AreEqual(expected, read(tls), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentTlsOptions))]
    public void TlsOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyTlsOptions, object?> read)
    {
        EasyTlsOptions tls = EasyOptionsFromCommandLine.TlsOf(Parse());

        Assert.AreEqual(read(LibcurlTlsDefaults), read(tls), member);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TlsOf_CertificateWithDriveLetterOnWindows_KeepsTheDriveColon()
    {
        EasyTlsOptions tls = EasyOptionsFromCommandLine.TlsOf(Parse("--cert", @"C:\certs\client.pem:secret"));

        Assert.AreEqual(@"C:\certs\client.pem", tls.Origin.Certificate);
        Assert.AreEqual("secret", tls.Origin.KeyPassword);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TlsOf_CertificateWithDriveLetterOffWindows_SplitsAtTheFirstColon()
    {
        EasyTlsOptions tls = EasyOptionsFromCommandLine.TlsOf(Parse("--cert", @"C:\certs\client.pem:secret"));

        Assert.AreEqual("C", tls.Origin.Certificate);
        Assert.AreEqual(@"\certs\client.pem:secret", tls.Origin.KeyPassword);
    }

    [TestMethod]
    public void TlsOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.TlsOf(null!));
    }

    private static object?[] GivenTls(string option, Func<EasyTlsOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentTls(string member, Func<EasyTlsOptions, object?> read) => [member, read];
}
