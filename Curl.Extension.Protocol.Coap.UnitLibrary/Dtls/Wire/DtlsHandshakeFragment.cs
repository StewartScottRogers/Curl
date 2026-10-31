using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Wire;

/// <summary>
/// One DTLS handshake message fragment: its 12-byte header and the bytes of the message body it
/// carries (RFC 6347 sections 4.2.2 and 4.2.3).
/// </summary>
/// <param name="Header">The fragment's header.</param>
/// <param name="Body">The <see cref="DtlsHandshakeHeader.FragmentLength" /> bytes of the message body.</param>
internal sealed record DtlsHandshakeFragment(DtlsHandshakeHeader Header, byte[] Body)
{
    /// <summary>Writes the header followed by the fragment's bytes.</summary>
    /// <exception cref="ArgumentException">The body's length is not the header's fragment length.</exception>
    public byte[] Encode()
    {
        if (Body.Length != Header.FragmentLength)
        {
            throw new ArgumentException($"A {Body.Length}-byte fragment body does not match its header's fragment length of {Header.FragmentLength}.", nameof(Body));
        }

        return [.. Header.Encode(), .. Body];
    }

    /// <summary>
    /// Splits one handshake message body into fragments of at most
    /// <paramref name="maximumFragmentLength" /> body bytes each, in order (RFC 6347 section 4.2.3).
    /// An empty body is one empty fragment.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is not positive or the body is past 2^24 - 1 bytes.</exception>
    public static IReadOnlyList<DtlsHandshakeFragment> Split(HandshakeType messageType, ushort messageSequence, byte[] body, int maximumFragmentLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFragmentLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(body.Length, DtlsHandshakeHeader.MaximumUInt24, nameof(body));
        List<DtlsHandshakeFragment> fragments = [];
        int offset = 0;
        do
        {
            int length = Math.Min(maximumFragmentLength, body.Length - offset);
            fragments.Add(new DtlsHandshakeFragment(
                new DtlsHandshakeHeader(messageType, body.Length, messageSequence, offset, length),
                body[offset..(offset + length)]));
            offset += length;
        }
        while (offset < body.Length);
        return fragments;
    }

    /// <summary>
    /// Reads the fragment at the start of <paramref name="bytes" />, a handshake record's
    /// plaintext that may hold several, and says how many bytes it took.
    /// </summary>
    /// <exception cref="FormatException">The header is malformed or the fragment runs past the bytes.</exception>
    public static DtlsHandshakeFragment Decode(ReadOnlySpan<byte> bytes, out int bytesRead)
    {
        DtlsHandshakeHeader header = DtlsHandshakeHeader.Decode(bytes);
        bytesRead = DtlsHandshakeHeader.HeaderLength + header.FragmentLength;
        if (bytesRead > bytes.Length)
        {
            throw new FormatException($"A DTLS handshake fragment of {header.FragmentLength} bytes runs past the {bytes.Length - DtlsHandshakeHeader.HeaderLength} bytes after its header.");
        }

        return new DtlsHandshakeFragment(header, bytes[DtlsHandshakeHeader.HeaderLength..bytesRead].ToArray());
    }
}
