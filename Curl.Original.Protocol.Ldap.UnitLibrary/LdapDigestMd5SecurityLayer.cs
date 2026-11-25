using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// The RFC 2831 section 2.3 confidentiality layer of a DIGEST-MD5 bind that negotiated
/// <c>qop=auth-conf</c> with the <c>3des</c> cipher (or <c>rc4</c>, <c>rc4-56</c>, <c>rc4-40</c>, BL-5452): each message is sent as
/// <c>3DES-CBC(message, padding, HMAC-MD5 first 10 bytes)</c>, then the version <c>0x0001</c> and a
/// 4-byte sequence number. The keys derive from H(A1), the MD5 of the response's A1 (BL-5376).
/// </summary>
internal sealed class LdapDigestMd5SecurityLayer : ILdapLogonAuthentication
{
    private const int BlockLength = 8;

    private const int MacLength = 10;

    private const int TrailerLength = 6;

    private readonly byte[] sendIntegrityKey;

    private readonly byte[] receiveIntegrityKey;

    private readonly byte[] sendKey;

    private readonly byte[] receiveKey;

    private readonly LdapRc4? sendStream;

    private readonly LdapRc4? receiveStream;

    private byte[] sendVector;

    private byte[] receiveVector;

    private uint sendSequence;

    private uint receiveSequence;

    private LdapDigestMd5SecurityLayer(byte[] hashA1, bool isServer, int maxBuffer, string cipher)
    {
        int keyLength = SealingKeyLength(cipher);
        isStream = cipher.StartsWith("rc4", StringComparison.Ordinal);
        MaxPlainLength = isStream
            ? Math.Max(1, maxBuffer - TrailerLength - MacLength)
            : Math.Max(1, (Math.Max(0, maxBuffer - TrailerLength) / BlockLength * BlockLength) - MacLength - 1);
        string sendDirection = isServer ? "server-to-client" : "client-to-server";
        string receiveDirection = isServer ? "client-to-server" : "server-to-client";
        sendIntegrityKey = Magic(hashA1, $"Digest session key to {sendDirection} signing key magic constant");
        receiveIntegrityKey = Magic(hashA1, $"Digest session key to {receiveDirection} signing key magic constant");
        byte[] sendSealing = Magic(hashA1[..keyLength], $"Digest H(A1) to {sendDirection} sealing key magic constant");
        byte[] receiveSealing = Magic(hashA1[..keyLength], $"Digest H(A1) to {receiveDirection} sealing key magic constant");
        if (isStream)
        {
            sendKey = [];
            receiveKey = [];
            sendVector = [];
            receiveVector = [];
            sendStream = new LdapRc4(sendSealing);
            receiveStream = new LdapRc4(receiveSealing);
            return;
        }

        sendKey = TripleDesKey(sendSealing);
        receiveKey = TripleDesKey(receiveSealing);
        sendVector = sendSealing[8..16];
        receiveVector = receiveSealing[8..16];
    }

    /// <summary>Creates the layer of a bind whose response was built from <paramref name="hashA1" />.</summary>
    /// <param name="isServer">Whether the layer sends as the server does; the tests' stand-in server is one.</param>
    /// <param name="hashA1">H(A1): the 16-byte MD5 of A1.</param>
    /// <returns>The layer.</returns>
    /// <param name="maxBuffer">The longest sealed buffer the peer accepts: the challenge's <c>maxbuf</c>.</param>
    /// <param name="cipher">The negotiated cipher: <c>3des</c>, <c>rc4</c>, <c>rc4-56</c> or <c>rc4-40</c>.</param>
    internal static LdapDigestMd5SecurityLayer Create(byte[] hashA1, bool isServer = false, int maxBuffer = DefaultMaxBuffer, string cipher = "3des") => new(hashA1, isServer, maxBuffer, cipher);

    // RFC 2831 section 2.4: how many bytes of H(A1) key the sealing key: 16, except 7 for rc4-56 and 5 for rc4-40.
    private static int SealingKeyLength(string cipher) => cipher switch
    {
        "rc4-56" => 7,
        "rc4-40" => 5,
        _ => TripleDesSealingLength,
    };

    private const int TripleDesSealingLength = 16;

    private readonly bool isStream;

    /// <summary>RFC 2831 section 2.4: the <c>maxbuf</c> when the challenge gives none.</summary>
    internal const int DefaultMaxBuffer = 65536;

    /// <summary>The longest message that <see cref="Wrap" /> seals into a buffer of at most the peer's maxbuf.</summary>
    internal int MaxPlainLength { get; }

    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public byte[]? NextToken(ReadOnlySpan<byte> challenge) => null;

    /// <inheritdoc />
    public byte[] Wrap(ReadOnlySpan<byte> message)
    {
        byte[] sequence = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sequence, sendSequence);
        if (isStream)
        {
            sendSequence++;
            return [.. sendStream!.Transform([.. message, .. Mac(sendIntegrityKey, sequence, message)]), 0x00, 0x01, .. sequence];
        }

        int padLength = BlockLength - ((message.Length + MacLength) % BlockLength);
        byte[] plain = new byte[message.Length + padLength + MacLength];
        message.CopyTo(plain);
        plain.AsSpan(message.Length, padLength).Fill((byte)padLength);
        Mac(sendIntegrityKey, sequence, message).CopyTo(plain.AsSpan(message.Length + padLength));
        using TripleDES cipher = CipherFor(sendKey);
        byte[] encrypted = cipher.EncryptCbc(plain, sendVector, PaddingMode.None);
        sendVector = encrypted[^BlockLength..];
        sendSequence++;
        return [.. encrypted, 0x00, 0x01, .. sequence];
    }

    /// <inheritdoc />
    public byte[]? Unwrap(ReadOnlySpan<byte> wrapped)
    {
        int encryptedLength = wrapped.Length - TrailerLength;
        if (!HasValidTrailer(wrapped, encryptedLength))
        {
            return null;
        }

        if (isStream)
        {
            byte[] streamPlain = receiveStream!.Transform(wrapped[..encryptedLength]);
            byte[] streamMessage = streamPlain[..^MacLength];
            bool authentic = CryptographicOperations.FixedTimeEquals(Mac(receiveIntegrityKey, wrapped[(encryptedLength + 2)..].ToArray(), streamMessage), streamPlain.AsSpan(streamMessage.Length));
            receiveSequence += authentic ? 1u : 0u;
            return authentic ? streamMessage : null;
        }

        using TripleDES cipher = CipherFor(receiveKey);
        byte[] plain = cipher.DecryptCbc(wrapped[..encryptedLength], receiveVector, PaddingMode.None);
        receiveVector = wrapped[(encryptedLength - BlockLength)..encryptedLength].ToArray();
        byte[]? message = AuthenticMessage(plain, wrapped[(encryptedLength + 2)..].ToArray());
        receiveSequence += message is null ? 0u : 1u;
        return message;
    }

    // Whether there are at least two blocks of ciphertext, then the 0x0001 version and the expected sequence number.
    private bool HasValidTrailer(ReadOnlySpan<byte> wrapped, int encryptedLength) =>
        (isStream ? encryptedLength >= MacLength : encryptedLength >= 2 * BlockLength && encryptedLength % BlockLength == 0)
        && wrapped[encryptedLength] == 0
        && wrapped[encryptedLength + 1] == 1
        && BinaryPrimitives.ReadUInt32BigEndian(wrapped[(encryptedLength + 2)..]) == receiveSequence;

    // The message in the decrypted buffer, when its padding is 1 to 8 bytes and its MAC checks.
    private byte[]? AuthenticMessage(byte[] plain, byte[] sequence)
    {
        int padLength = plain[^(MacLength + 1)];
        if ((uint)(padLength - 1) >= BlockLength)
        {
            return null;
        }

        byte[] message = plain[..Math.Max(0, plain.Length - MacLength - padLength)];
        return CryptographicOperations.FixedTimeEquals(Mac(receiveIntegrityKey, sequence, message), plain.AsSpan(plain.Length - MacLength))
            ? message
            : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(sendKey);
        CryptographicOperations.ZeroMemory(receiveKey);
        CryptographicOperations.ZeroMemory(sendIntegrityKey);
        CryptographicOperations.ZeroMemory(receiveIntegrityKey);
    }

    // MD5(H(A1) + constant).
    private static byte[] Magic(byte[] hashA1, string constant) =>
        MD5.HashData([.. hashA1, .. Encoding.ASCII.GetBytes(constant)]);

    // HMAC-MD5(key, sequence + message) cut to its first 10 bytes.
    private static byte[] Mac(byte[] key, byte[] sequence, ReadOnlySpan<byte> message) =>
        HMACMD5.HashData(key, (byte[])[.. sequence, .. message])[..MacLength];

    // The two-key 3DES key K1 K2 K1, each DES key the 7 bytes of the sealing key expanded to 8 with odd parity.
    private static byte[] TripleDesKey(byte[] sealing)
    {
        byte[] first = ExpandDesKey(sealing.AsSpan(0, 7));
        byte[] second = ExpandDesKey(sealing.AsSpan(7, 7));
        return [.. first, .. second, .. first];
    }

    // Spreads 56 bits over 8 bytes, 7 bits each, high bits first, and sets each byte's low bit for odd parity.
    private static byte[] ExpandDesKey(ReadOnlySpan<byte> key)
    {
        byte[] expanded = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            int bits = 0;
            for (int j = 0; j < 7; j++)
            {
                int position = (i * 7) + j;
                bits = (bits << 1) | ((key[position / 8] >> (7 - (position % 8))) & 1);
            }

            expanded[i] = (byte)((bits << 1) | (int.PopCount(bits) % 2 == 0 ? 1 : 0));
        }

        return expanded;
    }

    private static TripleDES CipherFor(byte[] key)
    {
        TripleDES cipher = TripleDES.Create();
        cipher.Key = key;
        return cipher;
    }
}
