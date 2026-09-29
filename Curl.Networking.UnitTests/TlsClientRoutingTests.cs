namespace Curl.Networking;

/// <summary>
/// Pins ADR-0140's routing rule, one data row per option set: <c>SslStream</c> for every
/// option set it can honour, the hand-built client for each row of the ADR's table whose
/// option reaches <see cref="TlsClientOptions" />.
/// </summary>
[TestClass]
public sealed class TlsClientRoutingTests
{
    public static IEnumerable<object[]> PlainOptionSets =>
    [
        [new TlsClientOptions()],
        [new TlsClientOptions(Insecure: true)],
        [new TlsClientOptions(CaCertificateFile: "ca.pem", CaCertificateDirectory: "certs")],
        [new TlsClientOptions(ClientCertificate: "client.p12", Passphrase: "secret")],
        [new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256", Tls13Ciphers: "TLS_AES_128_GCM_SHA256")],
        [new TlsClientOptions(SkipRevocationCheck: true, RevocationCheckBestEffort: true, UseAlpn: false)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls10)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls11)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls12)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls13)],
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls12)],
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls13)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls10, MaximumVersion: TlsVersion.Tls12)],
        [new TlsClientOptions(AutoClientCertificate: true)],
    ];

    // ADR-0191's row: --cert-status, whose stapled response SslStream never exposes.
    public static IEnumerable<object[]> CertificateStatusOptionSets =>
    [
        [new TlsClientOptions(RequireCertificateStatus: true)],
        [new TlsClientOptions(Insecure: true, RequireCertificateStatus: true)],
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls12, RequireCertificateStatus: true)],
        [new TlsClientOptions(RequireCertificateStatus: true, AutoClientCertificate: true)],
    ];

    // ADR-0140's legacy-versions row: the range's maximum (--tls-max) is TLS 1.0 or 1.1.
    public static IEnumerable<object[]> LegacyVersionOptionSets =>
    [
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls10)],
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls11)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls10, MaximumVersion: TlsVersion.Tls10)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls10, MaximumVersion: TlsVersion.Tls11)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls11, MaximumVersion: TlsVersion.Tls11)],
        [new TlsClientOptions(Insecure: true, MaximumVersion: TlsVersion.Tls11)],
    ];

    [TestMethod]
    [DynamicData(nameof(PlainOptionSets))]
    public void Choose_WithAPlainOptionSet_IsSslStream(TlsClientOptions options) =>
        Assert.AreEqual(TlsClientRoute.SslStream, TlsClientRouting.Choose(options));

    [TestMethod]
    [DynamicData(nameof(LegacyVersionOptionSets))]
    public void Choose_WithACeilingOfTls10OrTls11_IsTheHandBuiltClient(TlsClientOptions options) =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(options));

    [TestMethod]
    [DynamicData(nameof(CertificateStatusOptionSets))]
    public void Choose_WithCertStatus_IsTheHandBuiltClient(TlsClientOptions options) =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(options));

    [TestMethod]
    public void Choose_WithNullOptions_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsClientRouting.Choose(null!));
}
