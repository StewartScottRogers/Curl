namespace Curl.HttpDatagram.UnitTests;

[TestClass]
public sealed class H3DatagramSettingTests
{
    [TestMethod]
    [DataRow(true, new byte[] { 0x33, 0x01 })]
    [DataRow(false, new byte[] { 0x33, 0x00 })]
    public void EncodeEntry_Enabled_WritesIdentifierAndValue(bool enabled, byte[] expected)
    {
        var bytes = H3DatagramSetting.EncodeEntry(enabled);

        CollectionAssert.AreEqual(expected, bytes);
    }

    [TestMethod]
    [DataRow(true, 1UL, true)]
    [DataRow(true, 0UL, false)]
    [DataRow(false, 1UL, false)]
    [DataRow(false, 0UL, false)]
    public void Negotiate_PeerValue_EnablesOnlyWhenBothSentOne(bool sentByThisEndpoint, ulong peerValue, bool expected)
    {
        var negotiated = H3DatagramSetting.Negotiate(sentByThisEndpoint, peerValue);

        Assert.AreEqual(expected, negotiated);
    }

    [TestMethod]
    public void Negotiate_PeerDidNotSendSetting_IsDisabled()
    {
        var negotiated = H3DatagramSetting.Negotiate(true, null);

        Assert.IsFalse(negotiated);
    }

    [TestMethod]
    public void IsEnabledByPeer_ValueAboveOne_ThrowsSettingsError()
    {
        var exception = Assert.ThrowsExactly<HttpDatagramException>(() => H3DatagramSetting.IsEnabledByPeer(2));

        Assert.AreEqual(HttpDatagramErrorCode.SettingsError, exception.ErrorCode);
        Assert.Contains("value 2", exception.Message);
    }

    [TestMethod]
    public void EnsureDatagramAllowed_Negotiated_DoesNotThrow()
    {
        H3DatagramSetting.EnsureDatagramAllowed(true);
    }

    [TestMethod]
    public void EnsureDatagramAllowed_NotNegotiated_ThrowsDatagramError()
    {
        var exception = Assert.ThrowsExactly<HttpDatagramException>(() => H3DatagramSetting.EnsureDatagramAllowed(false));

        Assert.AreEqual(HttpDatagramErrorCode.DatagramError, exception.ErrorCode);
    }
}
