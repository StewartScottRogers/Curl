using System.Buffers.Binary;

namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// Reads a DNS message front to back, throwing <see cref="FormatException" /> for a field that runs past its end.
/// </summary>
/// <param name="message">The message.</param>
internal ref struct DnsMessageReader(ReadOnlySpan<byte> message)
{
    private const int PointerMarker = 0xC0;
    private const int MaximumLabelLength = 63;

    private readonly ReadOnlySpan<byte> message = message;
    private int offset;

    /// <summary>Reads a big-endian 16-bit field.</summary>
    /// <returns>The field's value.</returns>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Read(2));

    /// <summary>Moves past <paramref name="count" /> octets.</summary>
    /// <param name="count">How many octets.</param>
    public void Skip(int count) => Read(count);

    /// <summary>Reads <paramref name="count" /> octets.</summary>
    /// <param name="count">How many octets.</param>
    /// <returns>The octets.</returns>
    public ReadOnlySpan<byte> Read(int count)
    {
        if (count > message.Length - offset)
        {
            throw new FormatException("The DNS message ends inside a field.");
        }

        ReadOnlySpan<byte> field = message.Slice(offset, count);
        offset += count;
        return field;
    }

    /// <summary>
    /// Moves past a name: labels up to the root's zero octet, or up to a two-octet compression pointer, which ends
    /// the name where it stands (RFC 1035 section 4.1.4).
    /// </summary>
    public void SkipName()
    {
        int length = Read(1)[0];
        while (length != 0)
        {
            if (length >= PointerMarker)
            {
                Skip(1);
                return;
            }

            if (length > MaximumLabelLength)
            {
                throw new FormatException("A DNS name has a label type RFC 1035 does not define.");
            }

            Skip(length);
            length = Read(1)[0];
        }
    }
}
