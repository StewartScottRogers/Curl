using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.Protocol.DnsQuery.Wire;

/// <summary>
/// Reads a DNS message front to back, throwing <see cref="FormatException" /> for a field that runs past its end
/// or a name RFC 1035 does not allow.
/// </summary>
/// <param name="message">The whole message, which compression pointers index into.</param>
internal ref struct DnsMessageReader(ReadOnlySpan<byte> message)
{
    private const int PointerMarker = 0xC0;
    private const int MaximumLabelLength = 63;
    private const int MaximumNameLength = 255;

    private readonly ReadOnlySpan<byte> message = message;

    /// <summary>Gets the offset of the next octet to read.</summary>
    public int Offset { get; private set; }

    /// <summary>Reads one octet.</summary>
    /// <returns>The octet.</returns>
    public byte ReadByte() => Read(1)[0];

    /// <summary>Reads a big-endian 16-bit field.</summary>
    /// <returns>The field's value.</returns>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Read(2));

    /// <summary>Reads a big-endian 32-bit field.</summary>
    /// <returns>The field's value.</returns>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Read(4));

    /// <summary>Reads <paramref name="count" /> octets.</summary>
    /// <param name="count">How many octets.</param>
    /// <returns>The octets.</returns>
    public ReadOnlySpan<byte> Read(int count)
    {
        ReadOnlySpan<byte> field = Slice(Offset, count);
        Offset += count;
        return field;
    }

    /// <summary>
    /// Reads a name in presentation format, ending in a dot, following compression pointers (RFC 1035 section 4.1.4).
    /// Each pointer must point before the labels that hold it, so a pointer loop fails rather than hangs.
    /// </summary>
    /// <returns>The name; <c>.</c> for the root.</returns>
    public string ReadName()
    {
        StringBuilder builder = new();
        int position = Offset;
        int pointerLimit = Offset;
        int wireLength = 1;
        int resumeOffset = -1;
        int length = Slice(position, 1)[0];
        while (length != 0)
        {
            if (length >= PointerMarker)
            {
                resumeOffset = resumeOffset < 0 ? position + 2 : resumeOffset;
                position = PointerTarget(position, length, pointerLimit);
                pointerLimit = position;
            }
            else
            {
                wireLength = AppendLabel(builder, position, length, wireLength);
                position += length + 1;
            }

            length = Slice(position, 1)[0];
        }

        Offset = resumeOffset < 0 ? position + 1 : resumeOffset;
        return builder.Length == 0 ? "." : builder.ToString();
    }

    private readonly int PointerTarget(int position, int length, int pointerLimit)
    {
        int target = ((length & 0x3F) << 8) | Slice(position + 1, 1)[0];
        return target < pointerLimit
            ? target
            : throw new FormatException("A DNS compression pointer does not point back to an earlier name.");
    }

    private readonly int AppendLabel(StringBuilder builder, int position, int length, int wireLength)
    {
        if (length > MaximumLabelLength)
        {
            throw new FormatException("A DNS name has a label type RFC 1035 does not define.");
        }

        int newLength = wireLength + length + 1;
        if (newLength > MaximumNameLength)
        {
            throw new FormatException("A DNS name is longer than 255 octets.");
        }

        DnsPresentation.AppendLabel(builder, Slice(position + 1, length));
        builder.Append('.');
        return newLength;
    }

    private readonly ReadOnlySpan<byte> Slice(int start, int count)
    {
        if (count > message.Length - start)
        {
            throw new FormatException("The DNS message ends inside a field.");
        }

        return message.Slice(start, count);
    }
}
