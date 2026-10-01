namespace Curl.Networking;

[TestClass]
public sealed class TlsSessionPackingTests
{
    // curl 8.21.0's vtls_spack.c layout, byte for byte.
    [TestMethod]
    public void Pack_EveryField_WritesCurlsLayout()
    {
        var session = new PackedTlsSession([0xAA, 0xBB], 0x0304, 0x0102030405060708, "h2", 0x4000, [0x01]);

        var packed = TlsSessionPacking.Pack(session);

        CollectionAssert.AreEqual(
            new byte[]
            {
                0x01,
                0x04, 0x00, 0x02, 0xAA, 0xBB,
                0x02, 0x03, 0x04,
                0x03, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x05, 0x00, 0x02, (byte)'h', (byte)'2',
                0x06, 0x00, 0x00, 0x40, 0x00,
                0x07, 0x00, 0x01, 0x01,
            },
            packed);
        var unpacked = TlsSessionPacking.Unpack(packed)!;
        CollectionAssert.AreEqual(session.SessionData, unpacked.SessionData);
        Assert.AreEqual((session.ProtocolId, session.ValidUntil, session.ApplicationProtocol, session.MaxEarlyData), (unpacked.ProtocolId, unpacked.ValidUntil, unpacked.ApplicationProtocol, unpacked.MaxEarlyData));
        CollectionAssert.AreEqual(session.QuicTransportParameters, unpacked.QuicTransportParameters);
    }

    [TestMethod]
    public void Pack_WithoutOptionalFields_LeavesThemOut()
    {
        var packed = TlsSessionPacking.Pack(new PackedTlsSession([0xAA], 0x0304, 1, null, 0, []));

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x04, 0x00, 0x01, 0xAA, 0x02, 0x03, 0x04, 0x03, 0, 0, 0, 0, 0, 0, 0, 1 }, packed);
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "empty")]
    [DataRow(new byte[] { 0x02 }, DisplayName = "wrong version")]
    [DataRow(new byte[] { 0x01, 0x09 }, DisplayName = "unknown tag")]
    [DataRow(new byte[] { 0x01, 0x04, 0x00 }, DisplayName = "length cut short")]
    [DataRow(new byte[] { 0x01, 0x04, 0x00, 0x02, 0xAA }, DisplayName = "data cut short")]
    [DataRow(new byte[] { 0x01, 0x02, 0x03 }, DisplayName = "IETF id cut short")]
    [DataRow(new byte[] { 0x01, 0x03, 0, 0, 0 }, DisplayName = "valid-until cut short")]
    [DataRow(new byte[] { 0x01, 0x06, 0, 0 }, DisplayName = "early data cut short")]
    public void Unpack_MalformedBytes_IsRefused(byte[] packed) =>
        Assert.IsNull(TlsSessionPacking.Unpack(packed));

    [TestMethod]
    public void Unpack_VersionOnly_IsAnEmptySession()
    {
        var session = TlsSessionPacking.Unpack([0x01])!;

        Assert.IsEmpty(session.SessionData);
        Assert.AreEqual(0, session.ProtocolId);
    }
}
