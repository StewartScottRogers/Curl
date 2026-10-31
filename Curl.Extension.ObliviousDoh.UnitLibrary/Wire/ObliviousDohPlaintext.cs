namespace Curl.Extension.ObliviousDoh.Wire;

/// <summary>
/// An <c>ObliviousDoHMessagePlaintext</c> (RFC 9230 section 6.1): a DNS message, then padding that must be all
/// zeros. A query is padded to a multiple of 128 octets, the block length RFC 8467 section 4.1 recommends for
/// queries, as RFC 9230 section 11 asks.
/// </summary>
internal static class ObliviousDohPlaintext
{
    /// <summary>The block length a query plaintext is padded to a multiple of (RFC 8467 section 4.1).</summary>
    internal const int PaddingBlockLength = 128;

    /// <summary>Writes <paramref name="dnsMessage" /> with zero padding up to a multiple of <see cref="PaddingBlockLength" />.</summary>
    /// <param name="dnsMessage">The DNS message.</param>
    /// <returns>The plaintext.</returns>
    internal static byte[] Encode(ReadOnlySpan<byte> dnsMessage)
    {
        int unpaddedLength = 2 + dnsMessage.Length + 2;
        int paddingLength = (PaddingBlockLength - (unpaddedLength % PaddingBlockLength)) % PaddingBlockLength;
        return [.. LengthPrefix.Prepend(dnsMessage), .. LengthPrefix.Prepend(new byte[paddingLength])];
    }

    /// <summary>Reads the DNS message out of a plaintext.</summary>
    /// <param name="plaintext">The plaintext.</param>
    /// <returns>The DNS message, or <see langword="null" /> when the plaintext is malformed or its padding is not all zeros.</returns>
    internal static byte[]? DecodeDnsMessage(ReadOnlySpan<byte> plaintext)
    {
        var reader = new LengthPrefixedReader(plaintext);
        return reader.TryReadVector(out ReadOnlySpan<byte> dnsMessage) && reader.TryReadVector(out ReadOnlySpan<byte> padding)
            && reader.IsEmpty && !padding.ContainsAnyExcept((byte)0)
            ? dnsMessage.ToArray()
            : null;
    }
}
