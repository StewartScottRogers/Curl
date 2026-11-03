using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Amqp0;

/// <summary>Writes AMQP 0-9-1 method arguments in the data types of section 4.2.5, in network byte order, written from the specification alone.</summary>
public sealed class Amqp0ArgumentWriter
{
    private readonly List<byte> bytes = [];

    /// <summary>The arguments written so far.</summary>
    public byte[] ToArray() => [.. bytes];

    /// <summary>Writes an octet (4.2.5.1), which also carries packed bits.</summary>
    public Amqp0ArgumentWriter Octet(byte value)
    {
        bytes.Add(value);
        return this;
    }

    /// <summary>Writes a 16-bit short integer (4.2.5.1).</summary>
    public Amqp0ArgumentWriter Short(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        bytes.AddRange(buffer);
        return this;
    }

    /// <summary>Writes a 32-bit long integer (4.2.5.1).</summary>
    public Amqp0ArgumentWriter Long(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer);
        return this;
    }

    /// <summary>Writes a short string (4.2.5.3): one length octet, then the UTF-8 bytes.</summary>
    public Amqp0ArgumentWriter ShortString(string value)
    {
        byte[] text = Encoding.UTF8.GetBytes(value);
        bytes.Add(checked((byte)text.Length));
        bytes.AddRange(text);
        return this;
    }

    /// <summary>Writes a long string (4.2.5.3): a 32-bit length, then the bytes.</summary>
    public Amqp0ArgumentWriter LongString(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Long((uint)value.Length);
        bytes.AddRange(value);
        return this;
    }

    /// <summary>Writes a field table (4.2.5.5) of long-string values ('S').</summary>
    public Amqp0ArgumentWriter Table(params (string Name, string Value)[] entries)
    {
        Amqp0ArgumentWriter table = new();
        foreach ((string name, string value) in entries)
        {
            table.ShortString(name).Octet((byte)'S').LongString(Encoding.UTF8.GetBytes(value));
        }

        return LongString(table.ToArray());
    }
}
