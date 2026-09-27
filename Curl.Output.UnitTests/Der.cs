using System.Formats.Asn1;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Builds ASN.1 bytes by hand for the certificate printer's tests, including encodings a
/// strict encoder would refuse.
/// </summary>
internal static class Der
{
    internal const byte Sequence = 0x30;

    internal const byte Set = 0x31;

    /// <summary>One element: the identifier byte, a definite length, and the content.</summary>
    internal static byte[] Element(byte identifier, params byte[][] content)
    {
        var body = content.SelectMany(part => part).ToArray();
        byte[] length = body.Length switch
        {
            < 0x80 => [(byte)body.Length],
            < 0x100 => [0x81, (byte)body.Length],
            _ => [0x82, (byte)(body.Length >> 8), (byte)body.Length],
        };
        return [identifier, .. length, .. body];
    }

    internal static byte[] Sequenced(params byte[][] content) => Element(Sequence, content);

    internal static byte[] Oid(string dotted)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteObjectIdentifier(dotted);
        return writer.Encode();
    }

    internal static byte[] Integer(params byte[] content) => Element(0x02, content);

    internal static byte[] Bits(params byte[] content) => Element(0x03, [0x00, .. content]);

    internal static byte[] Utf8(string text) => Element(0x0C, Encoding.UTF8.GetBytes(text));

    internal static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>A <c>Name</c> of one attribute per relative name.</summary>
    internal static byte[] Name(params (string Oid, byte[] Value)[] attributes) =>
        Sequenced([.. attributes.Select(attribute => Element(Set, Sequenced(Oid(attribute.Oid), attribute.Value)))]);
}
