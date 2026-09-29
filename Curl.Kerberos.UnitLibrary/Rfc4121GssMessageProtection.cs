using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// RFC 4121 section 4.2's per-message tokens, for every encryption type but
/// <c>rc4-hmac</c>: a 16-byte header (token ID, flags, filler, EC, RRC and a 64-bit
/// sequence number) followed by the checksum (MIC, and Wrap without confidentiality) or the
/// encryption of the message and a copy of the header (Wrap with confidentiality).
/// </summary>
/// <remarks>
/// It sends EC 0 when encrypting, as MIT does for these encryption types, and RRC 0 always;
/// it reads any RRC by rotating the token back, as Windows sends 28.
/// </remarks>
internal sealed class Rfc4121GssMessageProtection : KerberosGssMessageProtection
{
    private const int HeaderSize = 16;
    private const ushort MicTokenId = 0x0404;
    private const ushort WrapTokenId = 0x0504;
    private const byte Filler = 0xFF;
    private const byte SentByAcceptor = 0x01;
    private const byte Sealed = 0x02;
    private const byte AcceptorSubkey = 0x04;
    private const int AcceptorSealUsage = 22;
    private const int AcceptorSignUsage = 23;
    private const int InitiatorSealUsage = 24;
    private const int InitiatorSignUsage = 25;

    private readonly KerberosEncryption encryption;
    private readonly byte sendFlags;
    private readonly byte receiveFlags;

    public Rfc4121GssMessageProtection(KerberosEncryption encryption, KerberosKey contextKey, bool acceptorSubkey, ulong sendSequence, ulong receiveSequence)
        : base(contextKey, sendSequence, receiveSequence)
    {
        this.encryption = encryption;
        sendFlags = acceptorSubkey ? AcceptorSubkey : (byte)0;
        receiveFlags = (byte)(sendFlags | SentByAcceptor);
    }

    public override byte[] GetMic(ReadOnlySpan<byte> message)
    {
        byte[] header = MicHeader(sendFlags, TakeSendSequence());
        return [.. header, .. encryption.ComputeChecksum(Key, InitiatorSignUsage, [.. message, .. header])];
    }

    public override void VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token)
    {
        if (token.Length != HeaderSize + encryption.ChecksumSize || !token[..8].SequenceEqual(MicHeader(receiveFlags, 0).AsSpan(0, 8)))
        {
            throw KerberosGssToken.Malformed();
        }

        ReadOnlySpan<byte> header = token[..HeaderSize];
        RequireIntegrity(encryption.VerifyChecksum(Key, AcceptorSignUsage, [.. message, .. header], token[HeaderSize..]));
        AcceptReceiveSequence(BinaryPrimitives.ReadUInt64BigEndian(header[8..]));
    }

    public override byte[] Wrap(ReadOnlySpan<byte> message, bool encrypt)
    {
        ulong sequence = TakeSendSequence();
        if (encrypt)
        {
            byte[] sealedHeader = WrapHeader((byte)(sendFlags | Sealed), 0, 0, sequence);
            byte[] plaintext = [.. message, .. sealedHeader];
            byte[] cipher = encryption.Encrypt(Key, InitiatorSealUsage, plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return [.. sealedHeader, .. cipher];
        }

        byte[] checksum = encryption.ComputeChecksum(Key, InitiatorSignUsage, [.. message, .. WrapHeader(sendFlags, 0, 0, sequence)]);
        return [.. WrapHeader(sendFlags, (ushort)checksum.Length, 0, sequence), .. message, .. checksum];
    }

    public override KerberosGssUnwrapped Unwrap(ReadOnlySpan<byte> token)
    {
        if (token.Length <= HeaderSize || BinaryPrimitives.ReadUInt16BigEndian(token) != WrapTokenId
            || (token[2] & ~Sealed) != receiveFlags || token[3] != Filler)
        {
            throw KerberosGssToken.Malformed();
        }

        byte[] header = token[..HeaderSize].ToArray();
        ushort extraCount = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
        byte[] body = RotateLeft(token[HeaderSize..], BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6)));
        bool encrypted = (header[2] & Sealed) != 0;
        byte[] message = encrypted ? Unseal(header, extraCount, body) : Unsign(header, extraCount, body);
        AcceptReceiveSequence(BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8)));
        return new KerberosGssUnwrapped(message, encrypted);
    }

    private static byte[] MicHeader(byte flags, ulong sequence)
    {
        byte[] header = [0x04, 0x04, flags, Filler, Filler, Filler, Filler, Filler, 0, 0, 0, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), sequence);
        return header;
    }

    private static byte[] WrapHeader(byte flags, ushort extraCount, ushort rightRotationCount, ulong sequence)
    {
        byte[] header = [0x05, 0x04, flags, Filler, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), extraCount);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), rightRotationCount);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), sequence);
        return header;
    }

    /// <summary>Undoes the sender's right rotation by <paramref name="count" /> (RFC 4121 section 4.2.5).</summary>
    private static byte[] RotateLeft(ReadOnlySpan<byte> body, int count)
    {
        int shift = count % body.Length;
        return [.. body[shift..], .. body[..shift]];
    }

    private static void RequireIntegrity(bool intact)
    {
        if (!intact)
        {
            throw new KerberosGssException(KerberosGssError.IntegrityCheckFailed);
        }
    }

    /// <summary>Decrypts a sealed Wrap body and checks the header copy inside it matches the header, RRC aside.</summary>
    private byte[] Unseal(byte[] header, ushort extraCount, byte[] body)
    {
        byte[] plaintext;
        try
        {
            plaintext = encryption.Decrypt(Key, AcceptorSealUsage, body);
        }
        catch (KerberosCryptographyException)
        {
            throw new KerberosGssException(KerberosGssError.IntegrityCheckFailed);
        }

        try
        {
            int messageLength = plaintext.Length - extraCount - HeaderSize;
            if (messageLength < 0)
            {
                throw KerberosGssToken.Malformed();
            }

            header[6] = 0;
            header[7] = 0;
            RequireIntegrity(plaintext.AsSpan(plaintext.Length - HeaderSize).SequenceEqual(header));
            return plaintext[..messageLength];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>Checks an unsealed Wrap body's trailing checksum, whose length EC gives, over the message and the header with EC and RRC zeroed.</summary>
    private byte[] Unsign(byte[] header, ushort extraCount, byte[] body)
    {
        if (extraCount != encryption.ChecksumSize || body.Length < extraCount)
        {
            throw KerberosGssToken.Malformed();
        }

        byte[] message = body[..^extraCount];
        header.AsSpan(4, 4).Clear();
        RequireIntegrity(encryption.VerifyChecksum(Key, AcceptorSignUsage, [.. message, .. header], body.AsSpan(message.Length)));
        return message;
    }
}
