using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Amqp0;

/// <summary>Reads AMQP 0-9-1 method arguments in the data types of section 4.2.5, written from the specification alone.</summary>
/// <param name="payload">The bytes to read.</param>
/// <param name="start">Where the first argument starts.</param>
public sealed class Amqp0ArgumentReader(byte[] payload, int start)
{
    private int position = start;

    /// <summary>Whether every byte has been read.</summary>
    public bool AtEnd => position == payload.Length;

    /// <summary>Reads an octet (4.2.5.1), which also carries packed bits.</summary>
    public byte Octet() => payload[position++];

    /// <summary>Reads a 16-bit short integer in network byte order (4.2.5.1).</summary>
    public ushort Short()
    {
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(position, 2));
        position += 2;
        return value;
    }

    /// <summary>Reads a 32-bit long integer in network byte order (4.2.5.1).</summary>
    public uint Long()
    {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(position, 4));
        position += 4;
        return value;
    }

    /// <summary>Reads a short string (4.2.5.3).</summary>
    public string ShortString()
    {
        int length = Octet();
        string value = Encoding.UTF8.GetString(payload, position, length);
        position += length;
        return value;
    }

    /// <summary>Reads a long string (4.2.5.3).</summary>
    public byte[] LongString()
    {
        int length = checked((int)Long());
        byte[] value = payload.AsSpan(position, length).ToArray();
        position += length;
        return value;
    }

    /// <summary>
    /// Reads a field table (4.2.5.5) whose values are long strings ('S', read as a string), booleans ('t') or
    /// field tables ('F'), in the order the peer wrote them.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object>> Table()
    {
        Amqp0ArgumentReader table = new(LongString(), 0);
        List<KeyValuePair<string, object>> entries = [];
        while (!table.AtEnd)
        {
            string name = table.ShortString();
            entries.Add(new(name, table.FieldValue()));
        }

        return entries;
    }

    private object FieldValue() => (char)Octet() switch
    {
        'S' => Encoding.UTF8.GetString(LongString()),
        't' => Octet() != 0,
        'F' => Table(),
        var type => throw new InvalidDataException($"The reference broker reads no field value of type '{type}'."),
    };
}
