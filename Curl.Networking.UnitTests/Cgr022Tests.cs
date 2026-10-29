using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-022 of the third-party gap report (BL-3551): curl 8.22.0's Schannel build reads
/// <c>--ciphers</c> as ALG_IDs (<c>CALG_*</c> names, decimal values, <c>USE_STRONG_CRYPTO</c>),
/// fails a list whose algorithms AcquireCredentialsHandle accepts none of with exit 35, refuses
/// an entry naming nothing with exit 59, and stops at TLS 1.2 (ADR-0896, measured).
/// </summary>
[TestClass]
public sealed class Cgr022Tests
{
    [TestMethod]
    [DataRow("CALG_AES_128")]
    [DataRow("CALG_AES_256")]
    [DataRow("USE_STRONG_CRYPTO")]
    [DataRow("SCH_USE_STRONG_CRYPTO")]
    [DataRow("USE_STRONG_CRYPTOX")]
    [DataRow("CALG_AES_128:CALG_SHA_256")]
    [DataRow("CALG_AES_128:CALG_3DES")]
    [DataRow("CALG_AES_128:")]
    [DataRow("26128")]
    [DataRow("26128:USE_STRONG_CRYPTO")]
    [DataRow("")]
    public void Refusal_ListSchannelAccepts_IsNone(string ciphers)
    {
        Assert.IsNull(SchannelAlgorithmList.Refusal(ciphers));
    }

    [TestMethod]
    public void Refusal_NoList_IsNone()
    {
        Assert.IsNull(SchannelAlgorithmList.Refusal(null));
    }

    [TestMethod]
    [DataRow("CALG_3DES")]
    [DataRow("CALG_AES")]
    [DataRow("CALG_RC4")]
    [DataRow("1:2")]
    public void Refusal_NoAlgorithmSchannelAccepts_IsAlgorithmMismatch(string ciphers)
    {
        var refusal = SchannelAlgorithmList.Refusal(ciphers);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.SslConnectError, refusal.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelAlgorithmMismatch, refusal.ErrorMessage);
    }

    [TestMethod]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256")]
    [DataRow("BOGUS")]
    [DataRow(":")]
    [DataRow("0")]
    [DataRow("0x6610")]
    [DataRow(" 26128")]
    [DataRow("CALG_AES_128 ")]
    [DataRow("CALG_NULLCIPHER")]
    [DataRow("CALG_ECDHE:CALG_AES_128")]
    [DataRow("99999999999")]
    public void Refusal_EntryNamingNoAlgorithm_IsCipherListRefused(string ciphers)
    {
        var refusal = SchannelAlgorithmList.Refusal(ciphers);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.SslCipher, refusal.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelCipherListRefused, refusal.ErrorMessage);
    }

    [TestMethod]
    public void Refusal_MoreThan47Algorithms_ReadsNoFurther()
    {
        var ciphers = string.Join(':', Enumerable.Repeat("CALG_3DES", 47)) + ":BOGUS";

        Assert.AreEqual(CurlExitCode.SslConnectError, SchannelAlgorithmList.Refusal(ciphers)!.ExitCode);
    }

    [TestMethod]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.SystemDefault, TlsVersion.Tls12)]
    [DataRow(TlsVersion.Tls12, TlsVersion.Tls13, TlsVersion.Tls12)]
    [DataRow(TlsVersion.Tls13, TlsVersion.SystemDefault, TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls11, TlsVersion.Tls11)]
    public void WithLegacyCredentialRange_SchannelBuildWithCiphers_StopsAtTls12(TlsVersion minimum, TlsVersion maximum, TlsVersion expected)
    {
        var options = new TlsClientOptions(MinimumVersion: minimum, MaximumVersion: maximum, Ciphers: "CALG_AES_128");

        Assert.AreEqual(expected, SchannelAlgorithmList.WithLegacyCredentialRange(options, matchesSchannelBuild: true).MaximumVersion);
    }

    [TestMethod]
    [DataRow(false, "CALG_AES_128")]
    [DataRow(true, null)]
    public void WithLegacyCredentialRange_OpenSslBuildOrNoCiphers_KeepsOptions(bool matchesSchannelBuild, string? ciphers)
    {
        var options = new TlsClientOptions(Ciphers: ciphers);

        Assert.AreSame(options, SchannelAlgorithmList.WithLegacyCredentialRange(options, matchesSchannelBuild));
    }
}
