using System.Formats.Asn1;
using System.Numerics;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Decodes an LDAPMessage holding an ExtendedResponse (RFC 4511 section 4.12), such as the
/// answer to the StartTLS extended request, into its result code.
/// </summary>
internal static class LdapExtendedResponse
{
    private static readonly Asn1Tag ExtendedResponseTag = new(TagClass.Application, 24, isConstructed: true);

    /// <summary>Decodes <paramref name="message" /> as the ExtendedResponse to the request with <paramref name="messageId" />.</summary>
    /// <param name="message">One whole LDAPMessage.</param>
    /// <param name="messageId">The ExtendedRequest's messageID.</param>
    /// <returns>
    /// The answer and its result code, in the shape a bind's answer has; <see cref="LdapBindReplyStatus.Malformed" />
    /// when the message is not an ExtendedResponse to that request.
    /// </returns>
    public static LdapBindReply Decode(ReadOnlyMemory<byte> message, int messageId)
    {
        try
        {
            AsnReader reader = new(message, AsnEncodingRules.BER);
            AsnReader ldapMessage = reader.ReadSequence();
            if (!ldapMessage.TryReadInt32(out int id) || id != messageId)
            {
                return Malformed;
            }

            AsnReader response = ldapMessage.ReadSequence(ExtendedResponseTag);
            BigInteger resultCode = new(response.ReadEnumeratedBytes().Span, isUnsigned: false, isBigEndian: true);
            return resultCode < 0 || resultCode > int.MaxValue ? Malformed : new LdapBindReply(LdapBindReplyStatus.Answered, (int)resultCode);
        }
        catch (AsnContentException)
        {
            return Malformed;
        }
    }

    private static LdapBindReply Malformed => new(LdapBindReplyStatus.Malformed, 0);
}
