using System.Text;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;

/// <summary>Builds DNS message octets field by field for the tests (RFC 1035 section 4.1).</summary>
internal sealed class DnsWire
{
    /// <summary>The offset of the first question's name, just after the 12-octet header.</summary>
    public const int FirstQuestionName = 12;

    private readonly List<byte> octets = [];

    public int Length => octets.Count;

    /// <summary>Gives a name's uncompressed wire form; an empty or <c>.</c> name is the root.</summary>
    public static byte[] Name(string dotted)
    {
        List<byte> wire = [];
        foreach (string label in dotted.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            wire.Add((byte)label.Length);
            wire.AddRange(Encoding.ASCII.GetBytes(label));
        }

        wire.Add(0);
        return [.. wire];
    }

    /// <summary>Gives a two-octet compression pointer to <paramref name="offset" />.</summary>
    public static byte[] Pointer(int offset) => [(byte)(0xC0 | (offset >> 8)), (byte)offset];

    /// <summary>Concatenates octet runs.</summary>
    public static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>Gives a big-endian 16-bit field.</summary>
    public static byte[] UInt16(int value) => [(byte)(value >> 8), (byte)value];

    /// <summary>Gives a big-endian 32-bit field.</summary>
    public static byte[] UInt32(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    /// <summary>Gives one character-string: its length octet, then its octets.</summary>
    public static byte[] CharacterString(string text) => [(byte)text.Length, .. Encoding.ASCII.GetBytes(text)];

    /// <summary>Gives a response to <c>example.com. IN A</c>, id 4660, flags qr rd ra, holding one answer owned by a pointer to the question's name.</summary>
    public static byte[] SingleAnswer(int type, byte[] data) =>
        new DnsWire()
            .Header(0x1234, 0x8180, 1, 1, 0, 0)
            .Question(Name("example.com"), 1, 1)
            .Record(Pointer(FirstQuestionName), type, 1, 300, data)
            .ToArray();

    public DnsWire Header(int id, int flags, int questions, int answers, int authority, int additional) =>
        Bytes(Join(UInt16(id), UInt16(flags), UInt16(questions), UInt16(answers), UInt16(authority), UInt16(additional)));

    public DnsWire Question(byte[] name, int type, int recordClass) => Bytes(Join(name, UInt16(type), UInt16(recordClass)));

    public DnsWire Record(byte[] owner, int type, int recordClass, uint timeToLive, byte[] data) =>
        Bytes(Join(owner, UInt16(type), UInt16(recordClass), UInt32(timeToLive), UInt16(data.Length), data));

    public DnsWire Bytes(byte[] run)
    {
        octets.AddRange(run);
        return this;
    }

    public byte[] ToArray() => [.. octets];
}
