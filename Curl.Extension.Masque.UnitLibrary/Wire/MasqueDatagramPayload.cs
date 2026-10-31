using Curl.HttpDatagram;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// The HTTP Datagram payload of a MASQUE tunnel: a Context ID, then the UDP payload
/// (RFC 9298 section 5) or the IP packet (RFC 9484 section 6). Curl uses only Context ID 0.
/// </summary>
internal static class MasqueDatagramPayload
{
    /// <summary>Prefixes a UDP payload or IP packet with Context ID 0.</summary>
    /// <param name="content">The UDP payload or IP packet.</param>
    /// <returns>The HTTP Datagram payload.</returns>
    public static byte[] Wrap(ReadOnlySpan<byte> content) => [0, .. content];

    /// <summary>Takes the content out of an HTTP Datagram payload; any Context ID but 0 is dropped, as both RFCs allow.</summary>
    /// <param name="payload">The HTTP Datagram payload.</param>
    /// <param name="content">The UDP payload or IP packet, or empty when dropped.</param>
    /// <returns><see langword="true" /> when the payload carried Context ID 0.</returns>
    public static bool TryUnwrap(ReadOnlyMemory<byte> payload, out ReadOnlyMemory<byte> content)
    {
        content = default;
        if (!HttpDatagramVariableLengthInteger.TryRead(payload.Span, out var contextId, out var consumed) || contextId != 0)
        {
            return false;
        }

        content = payload[consumed..];
        return true;
    }
}
