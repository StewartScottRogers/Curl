using Curl.Protocol.Abstractions;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("false")]
    [DataRow(null)]
    public void Of_WithEchOff_IsNull(string? mode)
    {
        var options = new TlsClientOptions(Ech: mode);
        Diagnostics.Arrange("options", options);
        Diagnostics.Arrange("offered configs", "public.test");

        var text = EchResultText.Of(options, Configs("public.test"), Host);

        Diagnostics.Act("result text", text ?? "(null)");
        Diagnostics.Assert("result text", "(null)", text ?? "(null)");
        Assert.IsNull(text);
    }

    [TestMethod]
    public void Of_WithGrease_IsSentGrease()
    {
        var expected = "status is sent GREASE, inner is NULL, outer is NULL";

        var text = WriteResultText(expected, new TlsClientOptions(Ech: "grease", EchConfigList: "AAA="), null);

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Of_WithGreaseAnsweredWithRetryConfigs_IsSentGreaseGotRetryConfigs()
    {
        var options = new TlsClientOptions(Ech: "grease");
        Diagnostics.Arrange("options", options);
        Diagnostics.Arrange("retry configs", "public.test");

        var text = EchResultText.Of(options, null, Host, Configs("public.test"));

        Diagnostics.Act("result text", text);
        Diagnostics.Diff("result text", "status is sent GREASE, got retry-configs, inner is NULL, outer is NULL", text ?? "(null)");
        Assert.AreEqual(
            "status is sent GREASE, got retry-configs, inner is NULL, outer is NULL",
            text);
    }

    [TestMethod]
    public void Of_WithTrueAndNothingOffered_IsNotConfigured()
    {
        var expected = "status is not configured, inner is NULL, outer is NULL";

        var text = WriteResultText(expected, new TlsClientOptions(Ech: "true"), null);

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Of_WithAnAcceptedOfferUnderInsecure_IsBadNameTolerated()
    {
        var expected = "status is bad name (tolerated without peer verification), inner is curl.test, outer is pn.test";

        var text = WriteResultText(expected, new TlsClientOptions(Insecure: true, Ech: "true", EchPublicName: "pn.test"), "pn.test");

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Of_WithAnAcceptedVerifiedOffer_IsSuccess()
    {
        var expected = "status is success, inner is curl.test, outer is public.test";

        var text = WriteResultText(expected, new TlsClientOptions(Ech: "hard", EchConfigList: "AAA="), "public.test");

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Of_WithAListOfNoSupportedConfig_IsNotConfigured()
    {
        var options = new TlsClientOptions(Ech: "true");
        Diagnostics.Arrange("options", options);
        Diagnostics.Arrange("offered configs", "public.test with KEM 0x9999");

        var text = EchResultText.Of(options, Configs("public.test", kemId: 0x9999), Host);

        Diagnostics.Act("result text", text);
        Diagnostics.Diff("result text", "status is not configured, inner is NULL, outer is NULL", text ?? "(null)");
        Assert.AreEqual(
            "status is not configured, inner is NULL, outer is NULL",
            text);
    }

    private string? WriteResultText(string expected, TlsClientOptions options, string? offeredPublicName)
    {
        Diagnostics.Arrange("options", options);
        Diagnostics.Arrange("offered configs", offeredPublicName ?? "(none)");

        var text = EchResultText.Of(options, offeredPublicName is null ? null : Configs(offeredPublicName), Host);

        Diagnostics.Act("result text", text);
        Diagnostics.Diff("result text", expected, text ?? "(null)");
        return text;
    }

    private static EchConfigList Configs(string publicName, ushort kemId = 0x0020) =>
        EchConfigList.Decode(HandBuiltTlsProviderTests.EchConfigListBytes(publicName, kemId)).Value!;
}
