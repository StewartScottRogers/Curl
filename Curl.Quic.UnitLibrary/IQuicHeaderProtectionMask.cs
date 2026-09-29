namespace Curl.Quic;

/// <summary>The header protection mask function of RFC 9001 section 5.4: five bytes from a 16-byte sample of the protected payload.</summary>
internal interface IQuicHeaderProtectionMask : IDisposable
{
    /// <summary>Writes the five mask bytes of <paramref name="sample" /> to <paramref name="mask" />.</summary>
    void ComputeMask(ReadOnlySpan<byte> sample, Span<byte> mask);
}
