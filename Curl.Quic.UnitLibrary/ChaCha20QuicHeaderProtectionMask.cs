using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Quic;

/// <summary>ChaCha20-based header protection (RFC 9001 section 5.4.4): the sample's first four bytes are the little-endian block counter, the other twelve the nonce, and the mask is ChaCha20 of five zero bytes.</summary>
internal sealed class ChaCha20QuicHeaderProtectionMask(byte[] headerProtectionKey) : IQuicHeaderProtectionMask
{
    private readonly byte[] key = (byte[])headerProtectionKey.Clone();

    public void ComputeMask(ReadOnlySpan<byte> sample, Span<byte> mask)
    {
        uint counter = BinaryPrimitives.ReadUInt32LittleEndian(sample);
        Span<byte> zeros = stackalloc byte[QuicHeaderProtection.MaskLength];
        zeros.Clear();
        ChaCha20.ApplyKeyStream(key, sample[4..QuicHeaderProtection.SampleLength], counter, zeros, mask[..QuicHeaderProtection.MaskLength]);
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}
