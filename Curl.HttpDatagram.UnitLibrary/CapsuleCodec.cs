namespace Curl.HttpDatagram;

/// <summary>
/// Encodes and decodes capsules in their RFC 9297 section 3.2 wire form:
/// Capsule Type (i), Capsule Length (i), Capsule Value (..).
/// </summary>
public static class CapsuleCodec
{
    /// <summary>
    /// Encodes one capsule.
    /// </summary>
    /// <param name="capsule">The capsule to encode.</param>
    /// <returns>Its wire bytes.</returns>
    public static byte[] Encode(Capsule capsule)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        var valueLength = (ulong)capsule.Value.Length;
        var typeLength = HttpDatagramVariableLengthInteger.GetEncodedLength(capsule.Type);
        var lengthLength = HttpDatagramVariableLengthInteger.GetEncodedLength(valueLength);
        var bytes = new byte[typeLength + lengthLength + capsule.Value.Length];
        HttpDatagramVariableLengthInteger.Write(capsule.Type, bytes);
        HttpDatagramVariableLengthInteger.Write(valueLength, bytes.AsSpan(typeLength));
        capsule.Value.Span.CopyTo(bytes.AsSpan(typeLength + lengthLength));
        return bytes;
    }

    /// <summary>
    /// Decodes one capsule from the start of <paramref name="source" />.
    /// </summary>
    /// <param name="source">Bytes read from the stream so far.</param>
    /// <param name="capsule">The capsule, or <see langword="null" /> when the source holds only part of it.</param>
    /// <param name="consumed">How many bytes the capsule took, or 0 when incomplete.</param>
    /// <returns><see langword="true" /> when the source held the whole capsule.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> source, out Capsule? capsule, out int consumed)
    {
        capsule = null;
        consumed = 0;
        if (!HttpDatagramVariableLengthInteger.TryRead(source, out var type, out var typeLength)
            || !HttpDatagramVariableLengthInteger.TryRead(source[typeLength..], out var valueLength, out var lengthLength))
        {
            return false;
        }

        var headerLength = typeLength + lengthLength;
        if ((ulong)(source.Length - headerLength) < valueLength)
        {
            return false;
        }

        var end = headerLength + (int)valueLength;
        capsule = new Capsule(type, source[headerLength..end].ToArray());
        consumed = end;
        return true;
    }
}
