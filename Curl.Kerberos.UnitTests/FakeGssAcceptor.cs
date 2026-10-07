using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// An in-memory GSS-API Kerberos acceptor for <see cref="FakeKdc.Service" /> with a fixed
/// session key. It reads the initial context token, answers with an AP-REP, and makes and
/// checks per-message tokens from the acceptor's side, all written here from RFC 2743,
/// RFC 4121 and RFC 4757 directly rather than with the library's GSS-API code, so the two
/// check each other. Its properties bend its answers for the failure tests.
/// </summary>
internal sealed class FakeGssAcceptor
{
    public static readonly byte[] MechanismOid = [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02];

    public FakeGssAcceptor(KerberosEncryptionType encryptionType)
    {
        EncryptionType = encryptionType;
        Encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(Enumerable.Repeat((byte)0x5A, 16).ToArray()));
        SessionKey = Enumerable.Repeat((byte)0x22, Encryption.KeySize).ToArray();
    }

    public KerberosEncryptionType EncryptionType { get; }

    public KerberosEncryption Encryption { get; }

    public byte[] SessionKey { get; }

    /// <summary>Gets or sets the subkey the AP-REP asserts; <see langword="null" /> for none.</summary>
    public byte[]? AcceptorSubkey { get; set; }

    /// <summary>Gets or sets the sequence number the AP-REP gives; <see langword="null" /> for none.</summary>
    public uint? AcceptorSequence { get; set; } = 0x00AB0000;

    /// <summary>Gets or sets how far off the AP-REP's echo of the authenticator's microseconds is.</summary>
    public int EchoedMicrosecondsOffset { get; set; }

    public KerberosApRequest? Request { get; private set; }

    public KerberosAuthenticator? Authenticator { get; private set; }

    /// <summary>Gets the key per-message tokens use: the acceptor's subkey when it asserted one, else the initiator's.</summary>
    public byte[] ContextKey => AcceptorSubkey ?? Authenticator!.Subkey!.Value.ToArray();

    public ulong InitiatorSequence => Authenticator!.SequenceNumber!.Value;

    public static byte[] Frame(byte[] inner)
    {
        byte[] contents = [.. MechanismOid, .. inner];
        byte[] length = contents.Length switch
        {
            < 0x80 => [(byte)contents.Length],
            < 0x100 => [0x81, (byte)contents.Length],
            _ => [0x82, (byte)(contents.Length >> 8), (byte)contents.Length],
        };
        return [0x60, .. length, .. contents];
    }

    public static byte[] Unframe(byte[] token)
    {
        Assert.AreEqual(0x60, token[0]);
        AsnDecoder.ReadEncodedValue(token, AsnEncodingRules.BER, out int offset, out int length, out int consumed);
        Assert.AreEqual(token.Length, consumed);
        byte[] contents = token.AsSpan(offset, length).ToArray();
        CollectionAssert.AreEqual(MechanismOid, contents[..MechanismOid.Length]);
        return contents[MechanismOid.Length..];
    }

    public KerberosCredential ServiceTicket(KerberosTicketFlags flags = KerberosTicketFlags.None) => new()
    {
        Client = FakeKdc.Alice,
        Server = FakeKdc.Service,
        Ticket = FakeKdc.TicketFor(new KerberosPrincipalName(2, ["HTTP", "server.example.test"])),
        SessionKey = new KerberosKey((int)EncryptionType, SessionKey.ToArray()),
        Flags = flags,
        AuthenticationTime = FakeKdc.Now,
        EndTime = FakeKdc.Now.AddHours(10),
    };

    /// <summary>Reads the initial context token: the framing, token ID 01 00, the AP-REQ and its authenticator.</summary>
    public void Accept(byte[] token)
    {
        DiagnosticAssertionLines.WriteExchangedMessage($"GSS initial context token (AP-REQ, etype {EncryptionType}, authenticator key usage 11)", token);
        byte[] inner = Unframe(token);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x00 }, inner[..2]);
        Request = KerberosApRequest.Decode(inner[2..]);
        Assert.AreEqual((int)EncryptionType, Request.Authenticator.EncryptionType);
        Authenticator = KerberosAuthenticator.Decode(Encryption.Decrypt(SessionKey, 11, Request.Authenticator.Cipher));
    }

    /// <summary>Makes the AP-REP token for the authenticator read by <see cref="Accept" />.</summary>
    public byte[] Reply(byte[]? replyKey = null)
    {
        KerberosEncryptedApReplyPart part = new()
        {
            ClientTime = Authenticator!.ClientTime,
            ClientMicroseconds = Authenticator.ClientMicroseconds + EchoedMicrosecondsOffset,
            Subkey = AcceptorSubkey is null ? null : new KerberosKey((int)EncryptionType, AcceptorSubkey.ToArray()),
            SequenceNumber = AcceptorSequence,
        };
        byte[] cipher = Encryption.Encrypt(replyKey ?? SessionKey, 12, part.Encode());
        byte[] token = ReplyToken(new KerberosApReply(new KerberosEncryptedData((int)EncryptionType, null, cipher)).Encode());
        DiagnosticAssertionLines.WriteExchangedMessage($"GSS reply token (AP-REP, etype {EncryptionType}, key usage 12, client time {part.ClientTime:O})", token);
        return token;
    }

    public static byte[] ReplyToken(byte[] body) => Frame([0x02, 0x00, .. body]);

    // RFC 4121 section 4.2, from the acceptor's side.
    public byte AcceptorFlags => (byte)(0x01 | (AcceptorSubkey is null ? 0 : 0x04));

    public static byte[] Rfc4121Header(ushort tokenId, byte flags, ushort extraCount, ushort rotation, ulong sequence)
    {
        byte[] header = new byte[16];
        BinaryPrimitives.WriteUInt16BigEndian(header, tokenId);
        header[2] = flags;
        header[3] = 0xFF;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), extraCount);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), rotation);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), sequence);
        return header;
    }

    public byte[] Rfc4121Mic(byte[] message, ulong sequence)
    {
        byte[] header = Rfc4121Header(0x0404, AcceptorFlags, 0xFFFF, 0xFFFF, sequence);
        header[3] = 0xFF;
        return [.. header, .. Encryption.ComputeChecksum(ContextKey, 23, [.. message, .. header])];
    }

    public byte[] Rfc4121Wrap(byte[] message, ulong sequence, bool encrypt, ushort rotation = 0, ushort? extraCount = null)
    {
        if (encrypt)
        {
            byte flags = (byte)(AcceptorFlags | 0x02);
            ushort ec = extraCount ?? 0;
            byte[] cipher = Encryption.Encrypt(ContextKey, 22, [.. message, .. new byte[Math.Min((int)ec, 64)], .. Rfc4121Header(0x0504, flags, ec, 0, sequence)]);
            return [.. Rfc4121Header(0x0504, flags, ec, rotation, sequence), .. RotateRight(cipher, rotation)];
        }

        byte[] checksum = Encryption.ComputeChecksum(ContextKey, 23, [.. message, .. Rfc4121Header(0x0504, AcceptorFlags, 0, 0, sequence)]);
        return [.. Rfc4121Header(0x0504, AcceptorFlags, extraCount ?? (ushort)checksum.Length, rotation, sequence), .. RotateRight([.. message, .. checksum], rotation)];
    }

    /// <summary>Reads the initiator's RFC 4121 Wrap token, checking its header, and gives its message and sequence number.</summary>
    public (byte[] Message, ulong Sequence, bool Encrypted) Rfc4121Unwrap(byte[] token)
    {
        Assert.AreEqual(0x0504, BinaryPrimitives.ReadUInt16BigEndian(token));
        byte flags = token[2];
        Assert.AreEqual(AcceptorFlags & 0x04, flags & 0x05, "SentByAcceptor clear, AcceptorSubkey as asserted");
        Assert.AreEqual(0xFF, token[3]);
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16BigEndian(token.AsSpan(6)), "RRC");
        ushort ec = BinaryPrimitives.ReadUInt16BigEndian(token.AsSpan(4));
        ulong sequence = BinaryPrimitives.ReadUInt64BigEndian(token.AsSpan(8));
        byte[] header = token[..16];
        if ((flags & 0x02) != 0)
        {
            Assert.AreEqual(0, ec);
            byte[] plaintext = Encryption.Decrypt(ContextKey, 24, token.AsSpan(16));
            CollectionAssert.AreEqual(header, plaintext[^16..]);
            return (plaintext[..^16], sequence, true);
        }

        Assert.AreEqual(Encryption.ChecksumSize, ec);
        byte[] message = token[16..^ec];
        header.AsSpan(4, 4).Clear();
        Assert.IsTrue(Encryption.VerifyChecksum(ContextKey, 25, [.. message, .. header], token.AsSpan(token.Length - ec)));
        return (message, sequence, false);
    }

    public ulong Rfc4121VerifyMic(byte[] message, byte[] token)
    {
        Assert.AreEqual(16 + Encryption.ChecksumSize, token.Length);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x04, (byte)(AcceptorFlags & 0x04), 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, token[..8]);
        Assert.IsTrue(Encryption.VerifyChecksum(ContextKey, 25, [.. message, .. token[..16]], token.AsSpan(16)));
        return BinaryPrimitives.ReadUInt64BigEndian(token.AsSpan(8));
    }

    // RFC 4757 section 7, from the acceptor's side.
    public static readonly byte[] Rc4MicHeader = [0x01, 0x01, 0x11, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

    public static readonly byte[] Rc4SealedWrapHeader = [0x02, 0x01, 0x11, 0x00, 0x10, 0x00, 0xFF, 0xFF];

    public static readonly byte[] Rc4SignedWrapHeader = [0x02, 0x01, 0x11, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

    public byte[] Rc4Mic(byte[] message, uint sequence, byte direction = 0xFF)
    {
        byte[] checksum = Rc4Checksum(15, [.. Rc4MicHeader, .. message]);
        return Frame([.. Rc4MicHeader, .. Rc4(SequenceKey(checksum), SequenceField(sequence, direction)), .. checksum]);
    }

    public byte[] Rc4Wrap(byte[] message, uint sequence, bool encrypt, byte[]? padding = null)
    {
        byte[] header = encrypt ? Rc4SealedWrapHeader : Rc4SignedWrapHeader;
        byte[] payload = [.. Enumerable.Repeat((byte)0x5A, 8), .. message, .. padding ?? [1]];
        byte[] checksum = Rc4Checksum(13, [.. header, .. payload]);
        byte[] sequenceField = SequenceField(sequence, 0xFF);
        byte[] body = encrypt ? Rc4(DataKey(sequenceField), payload) : payload;
        return Frame([.. header, .. Rc4(SequenceKey(checksum), sequenceField), .. checksum, .. body]);
    }

    /// <summary>Reads the initiator's RFC 4757 Wrap token and gives its message and sequence number.</summary>
    public (byte[] Message, uint Sequence, bool Encrypted) Rc4Unwrap(byte[] token)
    {
        byte[] inner = Unframe(token);
        byte[] header = inner[..8];
        bool encrypted = header.AsSpan().SequenceEqual(Rc4SealedWrapHeader);
        CollectionAssert.AreEqual(encrypted ? Rc4SealedWrapHeader : Rc4SignedWrapHeader, header);
        byte[] checksum = inner[16..24];
        byte[] sequenceField = Rc4(SequenceKey(checksum), inner[8..16]);
        CollectionAssert.AreEqual(new byte[4], sequenceField[4..], "initiator direction");
        byte[] payload = encrypted ? Rc4(DataKey(sequenceField), inner[24..]) : inner[24..];
        CollectionAssert.AreEqual(Rc4Checksum(13, [.. header, .. payload]), checksum);
        Assert.AreEqual(1, payload[^1], "one byte of padding");
        return (payload[8..^1], BinaryPrimitives.ReadUInt32BigEndian(sequenceField), encrypted);
    }

    public uint Rc4VerifyMic(byte[] message, byte[] token)
    {
        byte[] inner = Unframe(token);
        Assert.AreEqual(24, inner.Length);
        CollectionAssert.AreEqual(Rc4MicHeader, inner[..8]);
        byte[] checksum = inner[16..];
        CollectionAssert.AreEqual(Rc4Checksum(15, [.. Rc4MicHeader, .. message]), checksum);
        byte[] sequenceField = Rc4(SequenceKey(checksum), inner[8..16]);
        CollectionAssert.AreEqual(new byte[4], sequenceField[4..], "initiator direction");
        return BinaryPrimitives.ReadUInt32BigEndian(sequenceField);
    }

    private static byte[] RotateRight(byte[] data, int count)
    {
        int shift = count % data.Length;
        return [.. data[^shift..], .. data[..^shift]];
    }

    private static byte[] SequenceField(uint sequence, byte direction)
    {
        byte[] field = [0, 0, 0, 0, direction, direction, direction, direction];
        BinaryPrimitives.WriteUInt32BigEndian(field, sequence);
        return field;
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        byte[] result = new byte[data.Length];
        using Rc4 rc4 = new(key);
        rc4.ApplyKeyStream(data, result);
        return result;
    }

    private byte[] Rc4Checksum(int messageType, byte[] data)
    {
        byte[] typed = new byte[4 + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(typed, messageType);
        data.CopyTo(typed, 4);
        return HMACMD5.HashData(HMACMD5.HashData(ContextKey, "signaturekey\0"u8), MD5.HashData(typed))[..8];
    }

    private byte[] SequenceKey(byte[] checksum) => HMACMD5.HashData(HMACMD5.HashData(ContextKey, new byte[4]), checksum);

    private byte[] DataKey(byte[] sequenceField)
    {
        byte[] local = ContextKey.Select(value => (byte)(value ^ 0xF0)).ToArray();
        return HMACMD5.HashData(HMACMD5.HashData(local, new byte[4]), sequenceField[..4]);
    }
}
