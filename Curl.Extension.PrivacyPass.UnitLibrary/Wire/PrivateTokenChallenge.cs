using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.PrivacyPass.Wire;

/// <summary>
/// A decoded <c>TokenChallenge</c> (RFC 9577 §2.1): the token type, the issuer's name, the redemption context and the
/// origin information, with the bytes it was decoded from, whose SHA-256 digest the token carries (RFC 9577 §2.2).
/// </summary>
/// <param name="TokenType">The token type, such as <c>0x0002</c> for Blind RSA (RFC 9578 §6).</param>
/// <param name="IssuerName">The issuer's name.</param>
/// <param name="RedemptionContext">The redemption context: empty or 32 bytes.</param>
/// <param name="OriginInfo">The origin information, possibly empty.</param>
/// <param name="Encoded">The bytes the challenge was decoded from.</param>
public sealed record PrivateTokenChallenge(ushort TokenType, string IssuerName, byte[] RedemptionContext, string OriginInfo, byte[] Encoded)
{
    /// <summary>
    /// Decodes <paramref name="encoded"/>: <c>uint16 token_type</c>, <c>opaque issuer_name&lt;1..2^16-1&gt;</c>,
    /// <c>opaque redemption_context&lt;0..32&gt;</c> and <c>opaque origin_info&lt;0..2^16-1&gt;</c>, nothing after.
    /// </summary>
    /// <param name="encoded">The bytes of the challenge's <c>challenge</c> parameter.</param>
    /// <returns>The challenge, or <see langword="null"/> when the bytes are not one.</returns>
    public static PrivateTokenChallenge? Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        var reader = new TlsVectorReader(encoded);
        var read = reader.TryReadUInt16(out var tokenType)
            & reader.TryReadVector(2, out var issuerName)
            & reader.TryReadVector(1, out var redemptionContext)
            & reader.TryReadVector(2, out var originInfo);
        return read && reader.IsAtEnd && HasValidLengths(issuerName, redemptionContext)
            ? new(tokenType, Encoding.UTF8.GetString(issuerName), redemptionContext, Encoding.UTF8.GetString(originInfo), encoded)
            : null;
    }

    // A failed read leaves the reader where it was, so the reads after it fail or read nothing that is used.
    private static bool HasValidLengths(byte[] issuerName, byte[] redemptionContext) =>
        issuerName.Length > 0 && redemptionContext.Length is 0 or 32;

    /// <summary>Reads the TLS presentation language's integers and length-prefixed vectors (RFC 8446 §3.4).</summary>
    /// <param name="bytes">The bytes to read.</param>
    private ref struct TlsVectorReader(ReadOnlySpan<byte> bytes)
    {
        private ReadOnlySpan<byte> rest = bytes;

        public readonly bool IsAtEnd => rest.IsEmpty;

        public bool TryReadUInt16(out ushort value)
        {
            if (rest.Length < 2)
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadUInt16BigEndian(rest);
            rest = rest[2..];
            return true;
        }

        public bool TryReadVector(int lengthSize, out byte[] value)
        {
            value = [];
            if (rest.Length < lengthSize)
            {
                return false;
            }

            var length = lengthSize == 1 ? rest[0] : BinaryPrimitives.ReadUInt16BigEndian(rest);
            if (rest.Length - lengthSize < length)
            {
                return false;
            }

            value = rest.Slice(lengthSize, length).ToArray();
            rest = rest[(lengthSize + length)..];
            return true;
        }
    }
}
