using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking.UnitTests;

/// <summary>
/// Pins <see cref="SchannelAlgorithmList.OfferedSuites" />: a Schannel <c>--ciphers</c> ALG_ID
/// list restricts the offered suites class by class, as curl 8.22.0 Schannel does (BL-4911, ADR-1244).
/// </summary>
[TestClass]
public sealed class SchannelAlgorithmFilterTests
{
    private static readonly IReadOnlyList<ushort> Profile = ClientHelloProfile.Schannel.CipherSuites;

    [TestMethod]
    public void OfferedSuites_Aes128_KeepsOnlyAes128SuitesAndTheTls13Suites()
    {
        var (suites, failure) = SchannelAlgorithmList.OfferedSuites("CALG_AES_128", Profile);

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1301, 0xc02b, 0xc02f, 0xc023, 0xc027, 0xc009, 0xc013, 0x009c, 0x003c, 0x002f }, suites!.ToArray());
    }

    [TestMethod]
    public void OfferedSuites_Aes128WithSha256_KeepsTheSha256Aes128Suites()
    {
        var (suites, _) = SchannelAlgorithmList.OfferedSuites("CALG_AES_128:CALG_SHA_256", Profile);

        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1301, 0xc02b, 0xc02f, 0xc023, 0xc027, 0x009c, 0x003c }, suites!.ToArray());
    }

    [TestMethod]
    public void OfferedSuites_RsaKeyExchange_KeepsOnlyRsaKeyExchangeSuites()
    {
        var (suites, _) = SchannelAlgorithmList.OfferedSuites("CALG_RSA_KEYX", Profile);

        CollectionAssert.AreEqual(new ushort[] { 0x1302, 0x1301, 0x009d, 0x009c, 0x003d, 0x003c, 0x0035, 0x002f }, suites!.ToArray());
    }

    [TestMethod]
    public void OfferedSuites_NoSuiteLeft_IsSchannelsAlgorithmMismatch()
    {
        var (suites, failure) = SchannelAlgorithmList.OfferedSuites("CALG_ECDSA:CALG_RSA_KEYX", [0xc02b, 0xc02f]);

        Assert.IsNull(suites);
        Assert.AreEqual(CurlExitCode.SslConnectError, failure!.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelAlgorithmMismatch, failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("USE_STRONG_CRYPTO")]
    public void OfferedSuites_NoAcceptedAlgorithm_LeavesTheProfile(string? ciphers)
    {
        var (suites, failure) = SchannelAlgorithmList.OfferedSuites(ciphers, Profile);

        Assert.IsNull(suites);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void OfferedSuites_RefusedList_IsTheRefusal()
    {
        var (suites, failure) = SchannelAlgorithmList.OfferedSuites("ECDHE-RSA-AES128-GCM-SHA256", Profile);

        Assert.IsNull(suites);
        Assert.AreEqual(CurlExitCode.SslCipher, failure!.ExitCode);
    }

    [TestMethod]
    public void OfferedSuites_UnsupportedAlgorithmsOfARestrictedClass_AreNotOffered()
    {
        // CAMELLIA has no ALG_ID, DHE/DSS/MD5 are not on the profile but are classed all the same.
        var (suites, _) = SchannelAlgorithmList.OfferedSuites("CALG_AES_256:CALG_SHA1:CALG_RSA_SIGN", [0xc076, 0x0039, 0xc014, 0x0004]);

        CollectionAssert.AreEqual(new ushort[] { 0xc014, 0x0039 }.Order().ToArray(), suites!.Order().ToArray());
    }
}
