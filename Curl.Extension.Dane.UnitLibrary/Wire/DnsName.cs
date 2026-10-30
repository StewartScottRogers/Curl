using System.Text;

namespace Curl.Extension.Dane.Wire;

/// <summary>
/// A domain name as it was decoded, compression pointers followed (RFC 1035 section 4.1.4): its uncompressed wire
/// form, the length-prefixed labels ending in the root's zero octet (section 3.1) that canonical form (RFC 4034
/// section 6.2) lowercases, and its label count.
/// </summary>
public sealed class DnsName
{
    internal DnsName(byte[] wire, int labelCount)
    {
        Wire = wire;
        LabelCount = labelCount;
    }

    /// <summary>Gets the uncompressed wire form, in the letter case it arrived in.</summary>
    public ReadOnlyMemory<byte> Wire { get; }

    /// <summary>Gets how many labels the name has, not counting the root: 0 for the root itself.</summary>
    public int LabelCount { get; }

    /// <summary>
    /// The name's labels, each read as Latin-1 and followed by a dot, with no escaping; the root alone is ".".
    /// </summary>
    /// <returns>The name's text.</returns>
    public override string ToString()
    {
        if (LabelCount == 0)
        {
            return ".";
        }

        StringBuilder text = new();
        ReadOnlySpan<byte> wire = Wire.Span;
        for (int offset = 0; wire[offset] != 0; offset += wire[offset] + 1)
        {
            text.Append(Encoding.Latin1.GetString(wire.Slice(offset + 1, wire[offset]))).Append('.');
        }

        return text.ToString();
    }
}
