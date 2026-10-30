using System.Buffers.Binary;

namespace Curl.Extension.Dane.Wire;

/// <summary>
/// Reads a DNS message, or one record's RDATA, front to back, throwing <see cref="FormatException" /> for a field
/// that runs past its end or a name RFC 1035 does not allow.
/// </summary>
/// <param name="message">
/// The octets compression pointers count from: the whole message, or only the RDATA when its names must not be
/// compressed.
/// </param>
/// <param name="start">Where reading begins.</param>
/// <param name="end">Where reading must stop.</param>
internal ref struct DnsWireReader(ReadOnlySpan<byte> message, int start, int end)
{
    private const int PointerMarker = 0xC0;
    private const int MaximumLabelLength = 63;
    private const int MaximumNameLength = 255;

    private readonly ReadOnlySpan<byte> message = message;
    private readonly int end = end;
    private int offset = start;

    /// <summary>Initialises a reader over the whole of <paramref name="message" />.</summary>
    /// <param name="message">The octets to read.</param>
    public DnsWireReader(ReadOnlySpan<byte> message)
        : this(message, 0, message.Length)
    {
    }

    /// <summary>Gets where the next read begins.</summary>
    public readonly int Offset => offset;

    /// <summary>Gets how many octets are left before the end.</summary>
    public readonly int Remaining => end - offset;

    /// <summary>Reads one octet.</summary>
    /// <returns>The octet.</returns>
    public byte ReadByte() => Read(1)[0];

    /// <summary>Reads a big-endian 16-bit field.</summary>
    /// <returns>The field's value.</returns>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Read(2));

    /// <summary>Reads a big-endian 32-bit field.</summary>
    /// <returns>The field's value.</returns>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Read(4));

    /// <summary>Moves past <paramref name="count" /> octets.</summary>
    /// <param name="count">How many octets.</param>
    public void Skip(int count) => Read(count);

    /// <summary>Reads <paramref name="count" /> octets.</summary>
    /// <param name="count">How many octets.</param>
    /// <returns>The octets.</returns>
    public ReadOnlySpan<byte> Read(int count)
    {
        if (count > Remaining)
        {
            throw new FormatException("The DNS message ends inside a field.");
        }

        ReadOnlySpan<byte> field = message.Slice(offset, count);
        offset += count;
        return field;
    }

    /// <summary>
    /// Reads a name, following each compression pointer (RFC 1035 section 4.1.4) only backwards, so a loop of
    /// pointers cannot run forever.
    /// </summary>
    /// <param name="allowCompression">Whether a pointer may appear; RFC 4034 forbids one in RRSIG and NSEC RDATA.</param>
    /// <returns>The name, uncompressed.</returns>
    public DnsName ReadName(bool allowCompression)
    {
        List<byte> wire = [];
        int labels = 0;
        int position = offset;
        int resumeAt = -1;
        int length = LabelLengthAt(position);
        while (length != 0)
        {
            if (length >= PointerMarker)
            {
                int target = PointerTarget(position, allowCompression);
                resumeAt = resumeAt == -1 ? position + 2 : resumeAt;
                position = target;
            }
            else
            {
                AppendLabel(wire, position, length);
                labels++;
                position += length + 1;
            }

            length = LabelLengthAt(position);
        }

        wire.Add(0);
        offset = resumeAt == -1 ? position + 1 : resumeAt;
        return wire.Count > MaximumNameLength
            ? throw new FormatException("A DNS name is over 255 octets long.")
            : new DnsName([.. wire], labels);
    }

    private readonly int LabelLengthAt(int position) =>
        position < end ? message[position] : throw new FormatException("The DNS message ends inside a name.");

    private readonly int PointerTarget(int position, bool allowCompression)
    {
        if (!allowCompression)
        {
            throw new FormatException("A DNS name is compressed where RFC 4034 forbids it.");
        }

        int target = ((LabelLengthAt(position) & ~PointerMarker) << 8) | LabelLengthAt(position + 1);
        return target < position ? target : throw new FormatException("A DNS compression pointer does not point backwards.");
    }

    private readonly void AppendLabel(List<byte> wire, int position, int length)
    {
        if (length > MaximumLabelLength)
        {
            throw new FormatException("A DNS name has a label type RFC 1035 does not define.");
        }

        if (length >= end - position)
        {
            throw new FormatException("The DNS message ends inside a name.");
        }

        wire.Add((byte)length);
        wire.AddRange(message.Slice(position + 1, length));
    }
}
