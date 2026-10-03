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
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls12)],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls13)],
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls12)],
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls13)],
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

    // ADR-0151's row: --curves or --sigalgs, whose lists SslStream leaves to the operating system.
    public static IEnumerable<object[]> CurvesAndSigalgsOptionSets =>
    [
        [new TlsClientOptions(Curves: "X25519")],
        [new TlsClientOptions(SignatureAlgorithms: "ECDSA+SHA256")],
        [new TlsClientOptions(Curves: "P-384", SignatureAlgorithms: "rsa_pss_rsae_sha256")],
        [new TlsClientOptions(Insecure: true, MaximumVersion: TlsVersion.Tls12, Curves: "bogus")],
    ];

    [TestMethod]
    [DynamicData(nameof(CurvesAndSigalgsOptionSets))]
    public void Choose_WithCurvesOrSigalgs_IsTheHandBuiltClient(TlsClientOptions options) =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(options));

    // ADR-0319's row: --ssl-sessions, since SslStream can neither export nor import a session.
    // The --tls-earlydata row (BL-1105): SslStream sends no 0-RTT early data.
    [TestMethod]
    public void Choose_WithTlsEarlyData_IsTheHandBuiltClient() =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(new TlsClientOptions(AllowEarlyData: true)));

    [TestMethod]
    public void Choose_WithSslSessions_IsTheHandBuiltClient() =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(new TlsClientOptions(SslSessionsFile: "sessions.txt")));

    // ADR-0151's --no-sessionid row (BL-713): SslStream cannot stop the system's session cache.
    [TestMethod]
    public void Choose_WithNoSessionId_IsTheHandBuiltClient() =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(new TlsClientOptions(NoSessionId: true)));

    // ADR-0151's --ssl-allow-beast row (BL-713): only a range reaching TLS 1.0 can split, and a
    // TLS 1.0 or 1.1 ceiling is hand-built already, so a TLS 1.0 minimum below a higher one routes.
    [TestMethod]
    [DataRow(TlsVersion.Tls10, TlsClientRoute.HandBuilt)]
    [DataRow(TlsVersion.SystemDefault, TlsClientRoute.SslStream)]
    [DataRow(TlsVersion.Tls12, TlsClientRoute.SslStream)]
    public void Choose_WithSslAllowBeast_IsTheHandBuiltClientOnlyWhenTheRangeReachesTls10(TlsVersion minimum, TlsClientRoute expected) =>
        Assert.AreEqual(expected, TlsClientRouting.Choose(new TlsClientOptions(MinimumVersion: minimum, AllowBeast: true)));

    // ADR-0360's row (BL-1143): a TLS 1.0 or 1.1 minimum, alone or under a modern ceiling, since an
    // operating-system stack may refuse those versions.
    [TestMethod]
    [DataRow(TlsVersion.Tls10, TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls11, TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls12)]
    [DataRow(TlsVersion.Tls11, TlsVersion.Tls13)]
    public void Choose_WithATls10OrTls11Minimum_IsTheHandBuiltClient(TlsVersion minimum, TlsVersion maximum) =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(new TlsClientOptions(MinimumVersion: minimum, MaximumVersion: maximum)));

    // ADR-0327's row: --ech in any mode but false, since SslStream offers no Encrypted Client Hello.
    [TestMethod]
    [DataRow("grease", null)]
    [DataRow("true", null)]
    [DataRow("hard", null)]
    [DataRow(null, "AAA=")]
    [DataRow("false", "AAA=")]
    public void Choose_WithEch_IsTheHandBuiltClient(string? mode, string? configList) =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(new TlsClientOptions(Ech: mode, EchConfigList: configList)));

    // ADR-0359: pn: alone is hard, as libcurl's setopt_ech makes it.
    [TestMethod]
    public void Choose_WithOnlyAnEchPublicName_IsTheHandBuiltClient() =>
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(new TlsClientOptions(EchPublicName: "pn.test")));

    [TestMethod]
    [DataRow("false")]
    [DataRow("bogus")]
    [DataRow(null)]
    public void Choose_WithEchOff_IsSslStream(string? mode) =>
        Assert.AreEqual(TlsClientRoute.SslStream, TlsClientRouting.Choose(new TlsClientOptions(Ech: mode)));

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

    // ADR-0328's row: --tlsuser, since SslStream has no TLS-SRP; --tlspassword alone turns nothing on.
    [TestMethod]
    [DataRow("alice", "secret", TlsClientRoute.HandBuilt)]
    [DataRow("alice", null, TlsClientRoute.HandBuilt)]
    [DataRow(null, "secret", TlsClientRoute.SslStream)]
    public void Choose_WithTlsSrpOptions_IsTheHandBuiltClientOnlyWithATlsUser(string? user, string? password, TlsClientRoute expected) =>
        Assert.AreEqual(expected, TlsClientRouting.Choose(new TlsClientOptions(TlsUser: user, TlsPassword: password)));

    [TestMethod]
    public void Choose_WithNullOptions_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsClientRouting.Choose(null!));

    // BL-968: each row of ADR-0140's table names its option, the first row that holds winning.
    public static IEnumerable<object[]> ReasonsByRow =>
    [
        [new TlsClientOptions(MaximumVersion: TlsVersion.Tls11, RequireCertificateStatus: true), "--tls-max caps the versions below TLS 1.2"],
        [new TlsClientOptions(RequireCertificateStatus: true), "--cert-status asks for the stapled certificate status"],
        [new TlsClientOptions(Curves: "X25519"), "--curves or --sigalgs names the groups or signature algorithms"],
        [new TlsClientOptions(SslSessionsFile: "sessions.txt"), "--ssl-sessions imports and exports sessions"],
        [new TlsClientOptions(Ech: "grease"), "--ech offers Encrypted Client Hello"],
        [new TlsClientOptions(TlsUser: "alice"), "--tlsuser turns on TLS-SRP"],
        [new TlsClientOptions(NoSessionId: true), "--no-sessionid turns off the session cache"],
        [new TlsClientOptions(AllowBeast: true, MinimumVersion: TlsVersion.Tls10), "--ssl-allow-beast with a TLS 1.0 minimum turns off the CBC split"],
        [new TlsClientOptions(AllowEarlyData: true), "--tls-earlydata sends 0-RTT early data"],
        [new TlsClientOptions(MinimumVersion: TlsVersion.Tls11), "--tlsv1.0 or --tlsv1.1 lets the versions reach below TLS 1.2"],
    ];

    [TestMethod]
    [DynamicData(nameof(ReasonsByRow))]
    public void Reason_WhenARowHolds_NamesItsOption(TlsClientOptions options, string expected) =>
        Assert.AreEqual(expected, TlsClientRouting.Reason(options));

    [TestMethod]
    [DynamicData(nameof(PlainOptionSets))]
    public void Reason_WithAPlainOptionSet_IsNull(TlsClientOptions options) =>
        Assert.IsNull(TlsClientRouting.Reason(options));

    [TestMethod]
    public void Reason_WithNullOptions_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsClientRouting.Reason(null!));
}
