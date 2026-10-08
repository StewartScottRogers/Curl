using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class TlsSessionPackingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // curl 8.21.0's vtls_spack.c layout, byte for byte.
    [TestMethod]
    public void Pack_EveryField_WritesCurlsLayout()
    {
        var session = new PackedTlsSession([0xAA, 0xBB], 0x0304, 0x0102030405060708, "h2", 0x4000, [0x01]);
        Diagnostics.Arrange("session", session.ApplicationProtocol + ", protocol id 0x0304, max early data 0x4000");
        Diagnostics.Bytes("session data", session.SessionData);

        byte[] packed;
        using (Diagnostics.Phase("pack"))
        {
            packed = TlsSessionPacking.Pack(session);
        }

        Diagnostics.Bytes("packed", packed);
        Diagnostics.Act("packed length", packed.Length);

        var expectedPacked = new byte[]
        {
            0x01,
            0x04, 0x00, 0x02, 0xAA, 0xBB,
            0x02, 0x03, 0x04,
            0x03, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x05, 0x00, 0x02, (byte)'h', (byte)'2',
            0x06, 0x00, 0x00, 0x40, 0x00,
            0x07, 0x00, 0x01, 0x01,
        };
        Diagnostics.Diff("packed", expectedPacked, packed);
        Diagnostics.Assert("packed length", expectedPacked.Length, packed.Length);

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
        Diagnostics.Act("unpacked", $"{unpacked.ProtocolId}, {unpacked.ValidUntil}, {unpacked.ApplicationProtocol}, {unpacked.MaxEarlyData}");
        Diagnostics.Assert("round trip", $"{session.ProtocolId}, {session.ValidUntil}, {session.ApplicationProtocol}, {session.MaxEarlyData}", $"{unpacked.ProtocolId}, {unpacked.ValidUntil}, {unpacked.ApplicationProtocol}, {unpacked.MaxEarlyData}");
        CollectionAssert.AreEqual(session.SessionData, unpacked.SessionData);
        Assert.AreEqual((session.ProtocolId, session.ValidUntil, session.ApplicationProtocol, session.MaxEarlyData), (unpacked.ProtocolId, unpacked.ValidUntil, unpacked.ApplicationProtocol, unpacked.MaxEarlyData));
        CollectionAssert.AreEqual(session.QuicTransportParameters, unpacked.QuicTransportParameters);
    }

    [TestMethod]
    public void Pack_WithoutOptionalFields_LeavesThemOut()
    {
        Diagnostics.Arrange("session", "1 data byte, protocol id 0x0304, valid until 1, no ALPN, no early data, no QUIC parameters");

        var packed = TlsSessionPacking.Pack(new PackedTlsSession([0xAA], 0x0304, 1, null, 0, []));

        var expected = new byte[] { 0x01, 0x04, 0x00, 0x01, 0xAA, 0x02, 0x03, 0x04, 0x03, 0, 0, 0, 0, 0, 0, 0, 1 };
        Diagnostics.Bytes("packed", packed);
        Diagnostics.Act("packed length", packed.Length);
        Diagnostics.Diff("packed", expected, packed);
        Diagnostics.Assert("packed length", expected.Length, packed.Length);

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
    public void Unpack_MalformedBytes_IsRefused(byte[] packed)
    {
        Diagnostics.Arrange("packed length", packed.Length);
        Diagnostics.Bytes("packed", packed);

        var unpacked = TlsSessionPacking.Unpack(packed);

        Diagnostics.Act("unpacked", unpacked is null ? "null" : "session");
        Diagnostics.Assert("unpacked", "null", unpacked is null ? "null" : "session");

        Assert.IsNull(TlsSessionPacking.Unpack(packed));
    }

    [TestMethod]
    public void Unpack_VersionOnly_IsAnEmptySession()
    {
        Diagnostics.Arrange("packed", "version byte 0x01 only");

        var session = TlsSessionPacking.Unpack([0x01])!;

        Diagnostics.Act("session data length", session.SessionData.Length);
        Diagnostics.Act("protocol id", session.ProtocolId);
        Diagnostics.Assert("session data length", 0, session.SessionData.Length);
        Diagnostics.Assert("protocol id", 0, session.ProtocolId);

        Assert.IsEmpty(session.SessionData);
        Assert.AreEqual(0, session.ProtocolId);
    }
}
