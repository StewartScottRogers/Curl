namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="ServerHandshakeRecordReader" /> names the key-exchange group a server
/// chose from its plaintext handshake records (BL-4039): TLS 1.3's ServerHello <c>key_share</c>
/// or HelloRetryRequest <c>selected_group</c>, TLS 1.2's named-curve ServerKeyExchange, any
/// chunking, where it stops, and malformed input yielding no name.
/// </summary>
[TestClass]
public sealed class ServerHandshakeRecordReaderTests
{
    private const byte Handshake = 22;

    [TestMethod]
    [DataRow((ushort)0x0017, "secp256r1")]
    [DataRow((ushort)0x001d, "x25519")]
    [DataRow((ushort)0x11ec, "X25519MLKEM768")]
    [DataRow((ushort)0x0100, "ffdhe2048")]
    [DataRow((ushort)0x0201, "MLKEM768")]
    [DataRow((ushort)0x001f, "brainpoolP256r1tls13")]
    public void GroupName_Tls13ServerHelloKeyShare_IsOpenSslsName(ushort group, string expected)
    {
        var reader = Read(Record(Handshake, ServerHello(KeyShare(group, 32))));

        Assert.AreEqual(expected, reader.GroupName);
        Assert.IsFalse(reader.IsComplete);
    }

    [TestMethod]
    public void GroupName_BytesArriveOneAtATime_IsStillRead()
    {
        var reader = new ServerHandshakeRecordReader();
        foreach (var b in Record(Handshake, ServerHello(KeyShare(0x11ec, 1120))))
        {
            reader.Append([b]);
        }

        Assert.AreEqual("X25519MLKEM768", reader.GroupName);
    }

    [TestMethod]
    public void GroupName_HelloRetryRequestThenChangeCipherSpec_IsTheSelectedGroup()
    {
        var reader = Read(
            Record(Handshake, ServerHello(Extension(0x0033, [0x00, 0x17]))),
            Record(20, [1]),
            Record(Handshake, ServerHello(KeyShare(0x001d, 32))));

        Assert.AreEqual("secp256r1", reader.GroupName);
        Assert.IsTrue(reader.IsComplete);
    }

    [TestMethod]
    public void GroupName_KeyShareAfterOtherExtensions_IsFound()
    {
        var reader = Read(Record(Handshake, ServerHello([.. Extension(0x002b, [0x03, 0x04]), .. KeyShare(0x0018, 97)])));

        Assert.AreEqual("secp384r1", reader.GroupName);
    }

    [TestMethod]
    public void GroupName_HandshakeMessageSplitAcrossRecords_IsJoined()
    {
        var message = ServerHello(KeyShare(0x001d, 32));
        var reader = Read(Record(Handshake, message[..10]), Record(21, [1, 0]), Record(Handshake, message[10..]));

        Assert.AreEqual("x25519", reader.GroupName);
    }

    [TestMethod]
    public void GroupName_Tls12NamedCurveServerKeyExchange_IsTheCurve()
    {
        var reader = Read(Record(Handshake, [.. ServerHello(null), .. Message(11, [0, 0, 0]), .. Message(12, [3, 0x00, 0x17, 65])]));

        Assert.AreEqual("secp256r1", reader.GroupName);
    }

    [TestMethod]
    public void GroupName_Tls12ExplicitCurveServerKeyExchange_IsNull()
    {
        var reader = Read(Record(Handshake, [.. ServerHello(null), .. Message(12, [1, 0x00, 0x17, 65])]));

        Assert.IsNull(reader.GroupName);
    }

    [TestMethod]
    public void GroupName_RecordsAfterApplicationData_AreIgnored()
    {
        var reader = Read(Record(23, [1, 2, 3]), Record(Handshake, ServerHello(KeyShare(0x001d, 32))));

        Assert.IsTrue(reader.IsComplete);
        Assert.IsNull(reader.GroupName);
    }

    [TestMethod]
    public void Append_At64KiB_StopsTakingBytes()
    {
        var reader = Read(Record(21, new byte[ServerHandshakeRecordReader.MaximumBytes - 5]), Record(Handshake, ServerHello(KeyShare(0x001d, 32))));

        Assert.IsTrue(reader.IsComplete);
        Assert.IsNull(reader.GroupName);
    }

    [TestMethod]
    public void GroupName_PartialRecordHeader_IsNullAndNotComplete()
    {
        var reader = Read([Handshake, 3, 3]);

        Assert.IsFalse(reader.IsComplete);
        Assert.IsNull(reader.GroupName);
    }

    [TestMethod]
    public void GroupName_UnknownGroup_IsNull() =>
        Assert.IsNull(Read(Record(Handshake, ServerHello(KeyShare(0x9999, 32)))).GroupName);

    [TestMethod]
    [DynamicData(nameof(MalformedHandshakes))]
    public void GroupName_MalformedHandshake_IsNull(byte[] handshake) =>
        Assert.IsNull(Read(Record(Handshake, handshake)).GroupName);

    public static IEnumerable<object[]> MalformedHandshakes() =>
    [
        [Message(2, new byte[34])],
        [Message(2, [.. new byte[34], 40, 0, 0])],
        [Message(2, [.. new byte[34], 0, 0x13, 0x01, 0, 0, 10, 0, 0x33])],
        [Message(2, [.. new byte[34], 0, 0x13, 0x01, 0, 0, 5, 0, 0x33, 0, 4, 0])],
        [Message(2, [.. new byte[34], 0, 0x13, 0x01, 0, 0, 5, 0, 0x33, 0, 1, 0x17])],
        [Message(2, [.. new byte[34], 0, 0x13, 0x01, 0, 0, 4, 0, 0x2b, 0, 0])],
        [Message(12, [3, 0x00])],
        [ServerHello(KeyShare(0x001d, 32))[..20]],
    ];

    private static ServerHandshakeRecordReader Read(params byte[][] chunks)
    {
        var reader = new ServerHandshakeRecordReader();
        foreach (var chunk in chunks)
        {
            reader.Append(chunk);
        }

        return reader;
    }

    private static byte[] Record(byte contentType, byte[] payload) =>
        [contentType, 0x03, 0x03, (byte)(payload.Length >> 8), (byte)payload.Length, .. payload];

    private static byte[] Message(byte type, byte[] body) =>
        [type, (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length, .. body];

    private static byte[] ServerHello(byte[]? extensions)
    {
        byte[] fixedPart = [0x03, 0x03, .. new byte[32], 32, .. new byte[32], 0x13, 0x01, 0x00];
        return Message(2, extensions is null ? fixedPart : [.. fixedPart, (byte)(extensions.Length >> 8), (byte)extensions.Length, .. extensions]);
    }

    private static byte[] Extension(ushort type, byte[] data) =>
        [(byte)(type >> 8), (byte)type, (byte)(data.Length >> 8), (byte)data.Length, .. data];

    private static byte[] KeyShare(ushort group, int keyLength) =>
        Extension(0x0033, [(byte)(group >> 8), (byte)group, (byte)(keyLength >> 8), (byte)keyLength, .. new byte[keyLength]]);
}
