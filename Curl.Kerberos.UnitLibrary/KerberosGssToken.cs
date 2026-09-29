using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// RFC 2743 section 3.1's generic token framing for the Kerberos V5 mechanism: an
/// <c>[APPLICATION 0]</c> holding the mechanism OID <c>1.2.840.113554.1.2.2</c>, then the
/// two-byte token ID and the token's body. The context tokens (RFC 4121 section 4.1) and
/// <c>rc4-hmac</c>'s per-message tokens (RFC 4757 section 7) are framed; RFC 4121's own
/// per-message tokens are not.
/// </summary>
internal static class KerberosGssToken
{
    /// <summary>The token ID of the initial context token, which carries an AP-REQ.</summary>
    public const ushort ApRequestTokenId = 0x0100;

    /// <summary>The token ID of the acceptor's answer carrying an AP-REP.</summary>
    public const ushort ApReplyTokenId = 0x0200;

    /// <summary>The token ID of the acceptor's answer carrying a KRB-ERROR.</summary>
    public const ushort ErrorTokenId = 0x0300;

    private const byte FramingTag = 0x60;

    /// <summary>The DER encoding of the mechanism OID <c>1.2.840.113554.1.2.2</c>.</summary>
    private static readonly byte[] MechanismOid = [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x12, 0x01, 0x02, 0x02];

    private static readonly Asn1Tag PrimitiveApplicationZero = new(TagClass.Application, 0, isConstructed: false);

    /// <summary>Frames <paramref name="inner" />, which starts with its token ID.</summary>
    /// <param name="inner">The token ID and body.</param>
    /// <returns>The framed token.</returns>
    public static byte[] Frame(ReadOnlySpan<byte> inner)
    {
        byte[] contents = [.. MechanismOid, .. inner];
        AsnWriter writer = new(AsnEncodingRules.DER);

        // The framing is a constructed [APPLICATION 0] whose contents are not themselves
        // ASN.1, so it is written as a primitive one, which gives the same length octets,
        // and the tag byte is then marked constructed.
        writer.WriteOctetString(contents, PrimitiveApplicationZero);
        byte[] token = writer.Encode();
        token[0] = FramingTag;
        return token;
    }

    /// <summary>Frames <paramref name="body" /> behind <paramref name="tokenId" />.</summary>
    /// <param name="tokenId">The token ID.</param>
    /// <param name="body">The body.</param>
    /// <returns>The framed token.</returns>
    public static byte[] Frame(ushort tokenId, ReadOnlySpan<byte> body) => Frame([(byte)(tokenId >> 8), (byte)tokenId, .. body]);

    /// <summary>Reads the framing and gives what follows the mechanism OID: the token ID and body.</summary>
    /// <param name="token">The framed token.</param>
    /// <returns>The token ID and body.</returns>
    /// <exception cref="KerberosGssException">
    /// <see cref="KerberosGssError.MalformedToken" />: not one whole framed token, another
    /// mechanism, or no room for a token ID.
    /// </exception>
    public static ReadOnlySpan<byte> Unframe(ReadOnlySpan<byte> token)
    {
        int contentOffset;
        int contentLength;
        int bytesConsumed;
        try
        {
            AsnDecoder.ReadEncodedValue(token, AsnEncodingRules.BER, out contentOffset, out contentLength, out bytesConsumed);
        }
        catch (AsnContentException)
        {
            throw Malformed();
        }

        ReadOnlySpan<byte> contents = token.Slice(contentOffset, contentLength);
        bool framed = token[0] == FramingTag && bytesConsumed == token.Length
            && contents.Length >= MechanismOid.Length + sizeof(ushort) && contents.StartsWith(MechanismOid);
        return framed ? contents[MechanismOid.Length..] : throw Malformed();
    }

    /// <summary>Gives the token ID at the start of <paramref name="inner" />, which holds at least two bytes.</summary>
    /// <param name="inner">What <see cref="Unframe(ReadOnlySpan{byte})" /> gave.</param>
    /// <returns>The token ID.</returns>
    public static ushort TokenIdOf(ReadOnlySpan<byte> inner) => (ushort)((inner[0] << 8) | inner[1]);

    /// <summary>Makes the exception for a token that is not what was expected.</summary>
    /// <returns>A <see cref="KerberosGssError.MalformedToken" /> exception.</returns>
    public static KerberosGssException Malformed() => new(KerberosGssError.MalformedToken);
}
