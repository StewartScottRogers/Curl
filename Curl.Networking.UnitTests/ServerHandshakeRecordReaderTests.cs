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

    [TestMethod]
    [DataRow(0x0804, "RSASSA-PSS")]
    [DataRow(0x0805, "RSASSA-PSS")]
    [DataRow(0x0806, "RSASSA-PSS")]
    [DataRow(0x0809, "RSASSA-PSS")]
    [DataRow(0x080a, "RSASSA-PSS")]
    [DataRow(0x080b, "RSASSA-PSS")]
    [DataRow(0x0201, "rsaEncryption")]
    [DataRow(0x0401, "rsaEncryption")]
    [DataRow(0x0501, "rsaEncryption")]
    [DataRow(0x0601, "rsaEncryption")]
    [DataRow(0x0203, "id-ecPublicKey")]
    [DataRow(0x0403, "id-ecPublicKey")]
    [DataRow(0x0503, "id-ecPublicKey")]
    [DataRow(0x0603, "id-ecPublicKey")]
    [DataRow(0x081a, "id-ecPublicKey")]
    [DataRow(0x081b, "id-ecPublicKey")]
    [DataRow(0x081c, "id-ecPublicKey")]
    [DataRow(0x0807, "ED25519")]
    [DataRow(0x0808, "ED448")]
    public void PeerSignatureTypeName_Tls12EcdheServerKeyExchange_NamesTheScheme(int scheme, string expected)
    {
        var reader = Read(Record(Handshake, [.. ServerHello(null), .. Message(12, EcdheServerKeyExchange((ushort)scheme, 64))]));

        Assert.AreEqual(expected, reader.PeerSignatureTypeName(null));
    }

    [TestMethod]
    public void PeerSignatureTypeName_Tls12DheServerKeyExchange_NamesTheScheme()
    {
        byte[] parameters = [0x01, 0x00, .. new byte[256], 0x00, 0x01, 0x02, 0x01, 0x00, .. new byte[256]];
        var reader = Read(Record(Handshake, [.. ServerHello(null), .. Message(12, [.. parameters, .. Signature(0x0401, 256)])]));

        Assert.AreEqual("rsaEncryption", reader.PeerSignatureTypeName(null));
    }

    [TestMethod]
    public void PeerSignatureTypeName_Tls12WithACertificateKey_StillNamesTheScheme()
    {
        var reader = Read(Record(Handshake, [.. ServerHello(Extension(0x002b, [0x03, 0x03])), .. Message(12, EcdheServerKeyExchange(0x0401, 256))]));

        Assert.AreEqual("rsaEncryption", reader.PeerSignatureTypeName("1.2.840.113549.1.1.1"));
    }

    [TestMethod]
    public void PeerSignatureTypeName_Tls12WithoutServerKeyExchange_IsNull() =>
        Assert.IsNull(Read(Record(Handshake, [.. ServerHello(null), .. Message(11, [0, 0, 0])])).PeerSignatureTypeName("1.2.840.113549.1.1.1"));

    [TestMethod]
    public void PeerSignatureTypeName_Tls12UnknownScheme_IsNull() =>
        Assert.IsNull(Read(Record(Handshake, [.. ServerHello(null), .. Message(12, EcdheServerKeyExchange(0x0904, 64))])).PeerSignatureTypeName(null));

    [TestMethod]
    [DynamicData(nameof(MalformedServerKeyExchanges))]
    public void PeerSignatureTypeName_MalformedServerKeyExchange_IsNull(byte[] body) =>
        Assert.IsNull(Read(Record(Handshake, [.. ServerHello(null), .. Message(12, body)])).PeerSignatureTypeName(null));

    public static IEnumerable<object[]> MalformedServerKeyExchanges() =>
    [
        [(byte[])[3, 0x00, 0x17]],
        [(byte[])[3, 0x00, 0x17, 65, .. new byte[65], 0x04, 0x01]],
        [(byte[])[3, 0x00, 0x17, 1, 0, 0x04, 0x01, 0x00, 0x02, 0]],
        [(byte[])[3, 0x00, 0x17, 1, 0, 0x04, 0x01, 0x00, 0x01, 0, 0]],
        [(byte[])[0x00, 0x01, 0, 0x00, 0x01, 2]],
        [(byte[])[0x00, 0x01, 0, 0x00, 0x01, 2, 0x00, 0x01, 1]],
        [(byte[])[0x00, 0x05]],
    ];

    [TestMethod]
    [DataRow("1.2.840.113549.1.1.1", "RSASSA-PSS")]
    [DataRow("1.2.840.113549.1.1.10", "RSASSA-PSS")]
    [DataRow("1.2.840.10045.2.1", "id-ecPublicKey")]
    [DataRow("1.3.101.112", "ED25519")]
    [DataRow("1.3.101.113", "ED448")]
    public void PeerSignatureTypeName_Tls13_NamesTheCertificateKey(string keyAlgorithmOid, string expected)
    {
        var reader = Read(Record(Handshake, Tls13ServerHello()));

        Assert.AreEqual(expected, reader.PeerSignatureTypeName(keyAlgorithmOid));
    }

    [TestMethod]
    [DataRow("1.2.3.4")]
    [DataRow(null)]
    public void PeerSignatureTypeName_Tls13UnknownOrNoCertificateKey_IsNull(string? keyAlgorithmOid) =>
        Assert.IsNull(Read(Record(Handshake, Tls13ServerHello())).PeerSignatureTypeName(keyAlgorithmOid));

    [TestMethod]
    public void PeerSignatureTypeName_Tls13ServerHelloSplitAcrossChunks_NamesTheCertificateKey()
    {
        var record = Record(Handshake, Tls13ServerHello());
        var reader = Read(record[..7], record[7..50], record[50..]);

        Assert.AreEqual("id-ecPublicKey", reader.PeerSignatureTypeName("1.2.840.10045.2.1"));
    }

    [TestMethod]
    public void PeerSignatureTypeName_NothingRead_IsNull() =>
        Assert.IsNull(new ServerHandshakeRecordReader().PeerSignatureTypeName("1.2.840.113549.1.1.1"));

    private static byte[] Tls13ServerHello() =>
        ServerHello([.. Extension(0x002b, [0x03, 0x04]), .. KeyShare(0x001d, 32)]);

    private static byte[] EcdheServerKeyExchange(ushort scheme, int signatureLength) =>
        [3, 0x00, 0x17, 65, .. new byte[65], .. Signature(scheme, signatureLength)];

    private static byte[] Signature(ushort scheme, int length) =>
        [(byte)(scheme >> 8), (byte)scheme, (byte)(length >> 8), (byte)length, .. new byte[length]];

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
