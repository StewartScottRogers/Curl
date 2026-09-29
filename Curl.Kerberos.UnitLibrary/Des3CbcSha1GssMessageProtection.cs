using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// RFC 1964's per-message tokens for a <c>des3-cbc-sha1</c> context key, which RFC 4121
/// section 4.2 keeps for the encryption types defined before it, made as MIT's
/// <c>k5seal.c</c> and <c>k5unseal.c</c> make them: the framed header (token ID,
/// <c>SGN_ALG</c> 0x0004 for HMAC SHA1 DES3-KD, <c>SEAL_ALG</c> 0x0002 for DES3-KD or 0xFFFF
/// for none, filler), an 8-byte sequence number encrypted in triple-DES CBC under the key
/// with the checksum's first 8 bytes as IV, the 20-byte <c>hmac-sha1-des3-kd</c> checksum
/// for key usage 23 over the header and the data, and for Wrap an 8-byte confounder and the
/// message padded with 1 to 8 bytes each holding the padding's length, encrypted in
/// triple-DES CBC under the key with a zero IV when confidentiality is asked for.
/// </summary>
/// <remarks>
/// MIT gives the context key the type <c>des3-cbc-raw</c> for the sequence number and the
/// data (<c>kg_setup_keys</c>), so both are encrypted under the key itself, with no key
/// derivation, confounder or HMAC of their own. The sequence number is four little-endian
/// bytes followed by the direction: zeros from the initiator, 0xFF bytes from the acceptor.
/// </remarks>
internal sealed class Des3CbcSha1GssMessageProtection : KerberosGssMessageProtection
{
    private const int BlockSize = 8;
    private const int ChecksumSize = 20;
    private const int SignUsage = 23;
    private const int SequenceOffset = BlockSize;
    private const int ChecksumOffset = SequenceOffset + BlockSize;
    private const int DataOffset = ChecksumOffset + ChecksumSize;
    private const byte AcceptorDirection = 0xFF;

    private static readonly byte[] MicHeader = [0x01, 0x01, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];
    private static readonly byte[] SealedWrapHeader = [0x02, 0x01, 0x04, 0x00, 0x02, 0x00, 0xFF, 0xFF];
    private static readonly byte[] SignedWrapHeader = [0x02, 0x01, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

    private readonly Des3CbcSha1KerberosEncryption encryption;
    private readonly IKerberosRandomSource randomSource;

    public Des3CbcSha1GssMessageProtection(KerberosKey contextKey, ulong sendSequence, ulong receiveSequence, IKerberosRandomSource randomSource)
        : base(contextKey, sendSequence, receiveSequence)
    {
        encryption = new Des3CbcSha1KerberosEncryption(randomSource);
        this.randomSource = randomSource;
    }

    public override byte[] GetMic(ReadOnlySpan<byte> message)
    {
        byte[] checksum = encryption.ComputeChecksum(Key, SignUsage, [.. MicHeader, .. message]);
        return KerberosGssToken.Frame([.. MicHeader, .. EncryptSequence(checksum), .. checksum]);
    }

    public override void VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token)
    {
        ReadOnlySpan<byte> inner = KerberosGssToken.Unframe(token);
        if (inner.Length != DataOffset || !inner.StartsWith(MicHeader))
        {
            throw KerberosGssToken.Malformed();
        }

        ReadOnlySpan<byte> checksum = inner[ChecksumOffset..];
        RequireIntegrity(encryption.VerifyChecksum(Key, SignUsage, [.. MicHeader, .. message], checksum));
        AcceptSequence(checksum, inner[SequenceOffset..ChecksumOffset]);
    }

    public override byte[] Wrap(ReadOnlySpan<byte> message, bool encrypt)
    {
        byte[] header = encrypt ? SealedWrapHeader : SignedWrapHeader;
        int padding = BlockSize - (message.Length % BlockSize);
        byte[] plain = new byte[BlockSize + message.Length + padding];
        randomSource.Fill(plain.AsSpan(0, BlockSize));
        message.CopyTo(plain.AsSpan(BlockSize));
        plain.AsSpan(BlockSize + message.Length).Fill((byte)padding);
        byte[] checksum = encryption.ComputeChecksum(Key, SignUsage, [.. header, .. plain]);
        byte[] body = encrypt ? ApplyCbc(plain, new byte[BlockSize], encrypt: true) : plain;
        byte[] token = KerberosGssToken.Frame([.. header, .. EncryptSequence(checksum), .. checksum, .. body]);
        CryptographicOperations.ZeroMemory(plain);
        return token;
    }

    public override KerberosGssUnwrapped Unwrap(ReadOnlySpan<byte> token)
    {
        ReadOnlySpan<byte> inner = KerberosGssToken.Unframe(token);
        bool encrypted = IsSealedWrap(inner);
        ReadOnlySpan<byte> checksum = inner[ChecksumOffset..DataOffset];
        byte[] plain = encrypted ? ApplyCbc(inner[DataOffset..], new byte[BlockSize], encrypt: false) : inner[DataOffset..].ToArray();
        try
        {
            int padding = PaddingOf(plain);
            RequireIntegrity(encryption.VerifyChecksum(Key, SignUsage, [.. inner[..SequenceOffset], .. plain], checksum));
            AcceptSequence(checksum, inner[SequenceOffset..ChecksumOffset]);
            return new KerberosGssUnwrapped(plain[BlockSize..^padding], encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>Checks a Wrap token's header and that its data is a confounder and whole blocks, and gives whether it is sealed.</summary>
    private static bool IsSealedWrap(ReadOnlySpan<byte> inner)
    {
        bool sealedWrap = inner.StartsWith(SealedWrapHeader);
        bool wholeBlocks = inner.Length >= DataOffset + (2 * BlockSize) && (inner.Length - DataOffset) % BlockSize == 0;
        return wholeBlocks && (sealedWrap || inner.StartsWith(SignedWrapHeader)) ? sealedWrap : throw KerberosGssToken.Malformed();
    }

    /// <summary>The padding's length, which the last byte gives: 1 to 8, as MIT's <c>kg_unseal_v1</c> accepts.</summary>
    private static int PaddingOf(byte[] plain)
    {
        int padding = plain[^1];
        return padding is 0 or > BlockSize ? throw KerberosGssToken.Malformed() : padding;
    }

    private static void RequireIntegrity(bool intact)
    {
        if (!intact)
        {
            throw new KerberosGssException(KerberosGssError.IntegrityCheckFailed);
        }
    }

    /// <summary>Triple-DES CBC under the context key itself, MIT's <c>des3-cbc-raw</c>, over whole blocks.</summary>
    private byte[] ApplyCbc(ReadOnlySpan<byte> data, ReadOnlySpan<byte> initializationVector, bool encrypt)
    {
        using TripleDES tripleDes = TripleDES.Create();
        tripleDes.SetKey(Key);
        return encrypt
            ? tripleDes.EncryptCbc(data, initializationVector, PaddingMode.None)
            : tripleDes.DecryptCbc(data, initializationVector, PaddingMode.None);
    }

    /// <summary>The initiator's next sequence number and direction, encrypted with the checksum's first block as IV (MIT's <c>kg_make_seq_num</c>).</summary>
    private byte[] EncryptSequence(ReadOnlySpan<byte> checksum)
    {
        byte[] sequence = new byte[BlockSize];
        BinaryPrimitives.WriteUInt32LittleEndian(sequence, (uint)TakeSendSequence());
        return ApplyCbc(sequence, checksum[..BlockSize], encrypt: true);
    }

    /// <summary>Decrypts a sequence field and accepts it: the acceptor's direction and the next expected number (MIT's <c>kg_get_seq_num</c>).</summary>
    private void AcceptSequence(ReadOnlySpan<byte> checksum, ReadOnlySpan<byte> encryptedSequence)
    {
        byte[] sequence = ApplyCbc(encryptedSequence, checksum[..BlockSize], encrypt: false);
        if (sequence.AsSpan(sizeof(uint)).ContainsAnyExcept(AcceptorDirection))
        {
            throw new KerberosGssException(KerberosGssError.BadSequenceNumber);
        }

        AcceptReceiveSequence(BinaryPrimitives.ReadUInt32LittleEndian(sequence));
    }
}
