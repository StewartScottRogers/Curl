using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The text curl 8.21.0's OpenSSL ECH build writes after <c>ECH: result: </c> for a completed
/// handshake (ADR-0359, BL-1170).
/// </summary>
[TestClass]
public sealed class EchResultTextTests
{
    private const string Host = "curl.test";

    [TestMethod]
    [DataRow("false")]
    [DataRow(null)]
    public void Of_WithEchOff_IsNull(string? mode)
    {
        Assert.IsNull(EchResultText.Of(new TlsClientOptions(Ech: mode), Configs("public.test"), Host));
    }

    [TestMethod]
    public void Of_WithGrease_IsSentGrease()
    {
        Assert.AreEqual(
            "status is sent GREASE, inner is NULL, outer is NULL",
            EchResultText.Of(new TlsClientOptions(Ech: "grease", EchConfigList: "AAA="), null, Host));
    }

    [TestMethod]
    public void Of_WithGreaseAnsweredWithRetryConfigs_IsSentGreaseGotRetryConfigs()
    {
        Assert.AreEqual(
            "status is sent GREASE, got retry-configs, inner is NULL, outer is NULL",
            EchResultText.Of(new TlsClientOptions(Ech: "grease"), null, Host, Configs("public.test")));
    }

    [TestMethod]
    public void Of_WithTrueAndNothingOffered_IsNotConfigured()
    {
        Assert.AreEqual(
            "status is not configured, inner is NULL, outer is NULL",
            EchResultText.Of(new TlsClientOptions(Ech: "true"), null, Host));
    }

    [TestMethod]
    public void Of_WithAnAcceptedOfferUnderInsecure_IsBadNameTolerated()
    {
        Assert.AreEqual(
            "status is bad name (tolerated without peer verification), inner is curl.test, outer is pn.test",
            EchResultText.Of(new TlsClientOptions(Insecure: true, Ech: "true", EchPublicName: "pn.test"), Configs("pn.test"), Host));
    }

    [TestMethod]
    public void Of_WithAnAcceptedVerifiedOffer_IsSuccess()
    {
        Assert.AreEqual(
            "status is success, inner is curl.test, outer is public.test",
            EchResultText.Of(new TlsClientOptions(Ech: "hard", EchConfigList: "AAA="), Configs("public.test"), Host));
    }

    [TestMethod]
    public void Of_WithAListOfNoSupportedConfig_IsNotConfigured()
    {
        Assert.AreEqual(
            "status is not configured, inner is NULL, outer is NULL",
            EchResultText.Of(new TlsClientOptions(Ech: "true"), Configs("public.test", kemId: 0x9999), Host));
    }

    private static EchConfigList Configs(string publicName, ushort kemId = 0x0020) =>
        EchConfigList.Decode(HandBuiltTlsProviderTests.EchConfigListBytes(publicName, kemId)).Value!;
}
