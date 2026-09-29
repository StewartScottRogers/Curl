using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// RFC 4757 section 7's per-message tokens for an <c>rc4-hmac</c> context key: RFC 1964's
/// framed layout (token ID, <c>SGN_ALG</c> 0x0011 for HMAC-MD5, <c>SEAL_ALG</c> 0x0010 for RC4
/// or 0xFFFF for none, filler), an 8-byte sequence number RC4-encrypted under a key the
/// checksum picks, an 8-byte HMAC-MD5 checksum, and for Wrap an 8-byte confounder and the
/// message with one byte of padding, RC4-encrypted when confidentiality is asked for.
/// </summary>
/// <remarks>
/// The sequence number is four big-endian bytes followed by the direction: zeros from the
/// initiator, 0xFF bytes from the acceptor, as MIT writes it for this encryption type.
/// </remarks>
internal sealed class Rc4HmacGssMessageProtection : KerberosGssMessageProtection
{
    private const int FieldSize = 8;
    private const int MicMessageType = 15;
    private const int WrapMessageType = 13;
    private const byte AcceptorDirection = 0xFF;

    private static readonly byte[] MicHeader = [0x01, 0x01, 0x11, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];
    private static readonly byte[] SealedWrapHeader = [0x02, 0x01, 0x11, 0x00, 0x10, 0x00, 0xFF, 0xFF];
    private static readonly byte[] SignedWrapHeader = [0x02, 0x01, 0x11, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

    private readonly IKerberosRandomSource randomSource;

    public Rc4HmacGssMessageProtection(KerberosKey contextKey, ulong sendSequence, ulong receiveSequence, IKerberosRandomSource randomSource)
        : base(contextKey, sendSequence, receiveSequence)
    {
        this.randomSource = randomSource;
    }

    public override byte[] GetMic(ReadOnlySpan<byte> message)
    {
        byte[] checksum = Checksum(MicMessageType, MicHeader, message);
        return KerberosGssToken.Frame([.. MicHeader, .. EncryptSequence(checksum), .. checksum]);
    }

    public override void VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token)
    {
        ReadOnlySpan<byte> inner = KerberosGssToken.Unframe(token);
        if (inner.Length != MicHeader.Length + (2 * FieldSize) || !inner[..MicHeader.Length].SequenceEqual(MicHeader))
        {
            throw KerberosGssToken.Malformed();
        }

        ReadOnlySpan<byte> checksum = inner[^FieldSize..];
        byte[] sequence = ApplySequenceKey(checksum, inner.Slice(MicHeader.Length, FieldSize));
        RequireIntegrity(Checksum(MicMessageType, MicHeader, message), checksum);
        AcceptSequence(sequence);
    }

    public override byte[] Wrap(ReadOnlySpan<byte> message, bool encrypt)
    {
        byte[] header = encrypt ? SealedWrapHeader : SignedWrapHeader;
        byte[] payload = new byte[FieldSize + message.Length + 1];
        randomSource.Fill(payload.AsSpan(0, FieldSize));
        message.CopyTo(payload.AsSpan(FieldSize));
        payload[^1] = 1;
        byte[] checksum = Checksum(WrapMessageType, header, payload);
        byte[] sequence = EncryptSequence(checksum);
        if (encrypt)
        {
            byte[] plainSequence = ApplySequenceKey(checksum, sequence);
            ApplyDataKey(plainSequence, payload);
        }

        byte[] token = KerberosGssToken.Frame([.. header, .. sequence, .. checksum, .. payload]);
        CryptographicOperations.ZeroMemory(payload);
        return token;
    }

    public override KerberosGssUnwrapped Unwrap(ReadOnlySpan<byte> token)
    {
        ReadOnlySpan<byte> inner = KerberosGssToken.Unframe(token);
        bool encrypted = inner.StartsWith(SealedWrapHeader);
        if (inner.Length <= SealedWrapHeader.Length + (3 * FieldSize) || !(encrypted || inner.StartsWith(SignedWrapHeader)))
        {
            throw KerberosGssToken.Malformed();
        }

        ReadOnlySpan<byte> checksum = inner.Slice(SealedWrapHeader.Length + FieldSize, FieldSize);
        byte[] sequence = ApplySequenceKey(checksum, inner.Slice(SealedWrapHeader.Length, FieldSize));
        byte[] payload = inner[(SealedWrapHeader.Length + (2 * FieldSize))..].ToArray();
        try
        {
            if (encrypted)
            {
                ApplyDataKey(sequence, payload);
            }

            RequireIntegrity(Checksum(WrapMessageType, inner[..SealedWrapHeader.Length], payload), checksum);
            byte[] message = WithoutConfounderAndPadding(payload);
            AcceptSequence(sequence);
            return new KerberosGssUnwrapped(message, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    /// <summary>The message between the 8-byte confounder and the padding, whose length its last byte gives.</summary>
    private static byte[] WithoutConfounderAndPadding(byte[] payload)
    {
        int padding = payload[^1];
        return padding == 0 || padding > payload.Length - FieldSize
            ? throw KerberosGssToken.Malformed()
            : payload[FieldSize..^padding];
    }

    private static void RequireIntegrity(byte[] expected, ReadOnlySpan<byte> checksum)
    {
        if (!CryptographicOperations.FixedTimeEquals(expected, checksum))
        {
            throw new KerberosGssException(KerberosGssError.IntegrityCheckFailed);
        }
    }

    private static byte[] LittleEndian(int value)
    {
        byte[] bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static void ApplyKeyStream(byte[] key, Span<byte> data)
    {
        using Rc4 rc4 = new(key);
        rc4.ApplyKeyStream(data, data);
        CryptographicOperations.ZeroMemory(key);
    }

    /// <summary>
    /// <c>HMAC(HMAC(Kss, "signaturekey\0"), MD5(T | header | data))</c>, its first eight bytes,
    /// T the RFC 4757 message type as four little-endian bytes.
    /// </summary>
    private byte[] Checksum(int messageType, ReadOnlySpan<byte> header, ReadOnlySpan<byte> data)
    {
        byte[] signingKey = HMACMD5.HashData(Key, "signaturekey\0"u8);
        byte[] digest = MD5.HashData([.. LittleEndian(messageType), .. header, .. data]);
        byte[] checksum = HMACMD5.HashData(signingKey, digest)[..FieldSize];
        CryptographicOperations.ZeroMemory(signingKey);
        return checksum;
    }

    /// <summary>The initiator's next sequence number and direction, RC4-encrypted under the checksum's sequence key.</summary>
    private byte[] EncryptSequence(ReadOnlySpan<byte> checksum)
    {
        byte[] sequence = new byte[FieldSize];
        BinaryPrimitives.WriteUInt32BigEndian(sequence, (uint)TakeSendSequence());
        return ApplySequenceKey(checksum, sequence);
    }

    /// <summary>RC4s <paramref name="sequence" /> under <c>Kseq = HMAC(HMAC(Kss, 0), checksum)</c>; the same call encrypts and decrypts.</summary>
    private byte[] ApplySequenceKey(ReadOnlySpan<byte> checksum, ReadOnlySpan<byte> sequence)
    {
        byte[] zeroKey = HMACMD5.HashData(Key, LittleEndian(0));
        byte[] sequenceKey = HMACMD5.HashData(zeroKey, checksum);
        CryptographicOperations.ZeroMemory(zeroKey);
        byte[] result = sequence.ToArray();
        ApplyKeyStream(sequenceKey, result);
        return result;
    }

    /// <summary>RC4s the confounder and data in place under <c>Kcrypt = HMAC(HMAC(Kss XOR 0xF0, 0), seq)</c>, seq the four big-endian sequence bytes.</summary>
    private void ApplyDataKey(ReadOnlySpan<byte> plainSequence, Span<byte> payload)
    {
        byte[] localKey = Key.ToArray();
        for (int index = 0; index < localKey.Length; index++)
        {
            localKey[index] ^= 0xF0;
        }

        byte[] zeroKey = HMACMD5.HashData(localKey, LittleEndian(0));
        byte[] dataKey = HMACMD5.HashData(zeroKey, plainSequence[..sizeof(uint)]);
        CryptographicOperations.ZeroMemory(localKey);
        CryptographicOperations.ZeroMemory(zeroKey);
        ApplyKeyStream(dataKey, payload);
    }

    /// <summary>Accepts a decrypted sequence field: the acceptor's direction and the next expected number.</summary>
    private void AcceptSequence(byte[] sequence)
    {
        if (!sequence.AsSpan(sizeof(uint)).ContainsAnyExcept(AcceptorDirection))
        {
            AcceptReceiveSequence(BinaryPrimitives.ReadUInt32BigEndian(sequence));
            return;
        }

        throw new KerberosGssException(KerberosGssError.BadSequenceNumber);
    }
}
