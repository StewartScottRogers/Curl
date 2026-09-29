namespace Curl.Http2;

/// <summary>
/// One HTTP/2 frame as it crosses the wire (RFC 9113 section 4.1): the nine-byte header's
/// fields and the payload, still in its wire form, padding and all.
/// </summary>
/// <param name="Type">The frame type; an unknown type keeps its raw value.</param>
/// <param name="Flags">The flag bits; see <see cref="Http2FrameFlags" />.</param>
/// <param name="StreamId">The 31-bit stream identifier, 0 for the connection.</param>
/// <param name="Payload">The payload bytes.</param>
public sealed record Http2Frame(Http2FrameType Type, byte Flags, int StreamId, ReadOnlyMemory<byte> Payload)
{
    /// <summary>
    /// Returns whether every bit of <paramref name="flag" /> is set.
    /// </summary>
    /// <param name="flag">One of the <see cref="Http2FrameFlags" /> constants.</param>
    /// <returns><see langword="true" /> when the flag is set.</returns>
    public bool HasFlag(byte flag) => (Flags & flag) == flag;
}
