namespace Curl.HttpDatagram;

/// <summary>
/// Reassembles the capsules of an HTTP/1.1 or HTTP/2 stream that uses the Capsule Protocol
/// and hands over the payload of each <c>DATAGRAM</c> capsule, silently skipping capsules
/// of every other type as RFC 9297 section 3.2 requires.
/// </summary>
public sealed class DatagramCapsuleReader
{
    private byte[] _unreadBytes = [];

    /// <summary>
    /// Adds bytes read from the stream.
    /// </summary>
    /// <param name="bytes">The bytes, in stream order.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        _unreadBytes = [.. _unreadBytes, .. bytes];
    }

    /// <summary>
    /// Takes the payload of the next whole <c>DATAGRAM</c> capsule, dropping every capsule
    /// of another type before it.
    /// </summary>
    /// <param name="payload">The datagram's payload, or empty when none is whole yet.</param>
    /// <returns><see langword="true" /> when a datagram was taken.</returns>
    public bool TryReadDatagram(out ReadOnlyMemory<byte> payload)
    {
        payload = ReadOnlyMemory<byte>.Empty;
        while (CapsuleCodec.TryDecode(_unreadBytes, out var capsule, out var consumed))
        {
            _unreadBytes = _unreadBytes[consumed..];
            if (capsule!.Type == CapsuleType.Datagram)
            {
                payload = capsule.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks the stream ended on a capsule boundary, as RFC 9297 section 3.3 requires.
    /// </summary>
    /// <exception cref="HttpDatagramException">Bytes of an unfinished capsule are left.</exception>
    public void Complete()
    {
        if (_unreadBytes.Length != 0)
        {
            throw new HttpDatagramException(HttpDatagramErrorCode.MalformedCapsule, "the stream ended inside a capsule");
        }
    }
}
