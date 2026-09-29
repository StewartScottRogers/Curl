using System.Security.Cryptography;

namespace Curl.Quic;

/// <summary>AES-based header protection (RFC 9001 section 5.4.3): the first five bytes of AES-ECB of the sample under the header protection key.</summary>
internal sealed class AesQuicHeaderProtectionMask : IQuicHeaderProtectionMask
{
    private readonly Aes aes = Aes.Create();

    public AesQuicHeaderProtectionMask(byte[] headerProtectionKey) => aes.Key = headerProtectionKey;

    public void ComputeMask(ReadOnlySpan<byte> sample, Span<byte> mask)
    {
        Span<byte> block = stackalloc byte[QuicHeaderProtection.SampleLength];
        aes.EncryptEcb(sample, block, PaddingMode.None);
        block[..QuicHeaderProtection.MaskLength].CopyTo(mask);
    }

    public void Dispose() => aes.Dispose();
}
