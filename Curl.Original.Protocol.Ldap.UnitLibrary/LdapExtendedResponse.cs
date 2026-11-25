using System.Formats.Asn1;
using System.Numerics;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Decodes the answer to the StartTLS extended request into its result code: an LDAPMessage holding
/// an ExtendedResponse (RFC 4511 section 4.12), or any other response that opens with an LDAPResult,
/// which <c>libldap</c> reads the same way (BL-5931, ADR-1366).
/// </summary>
internal static class LdapExtendedResponse
{
    private const int SearchResultEntryTagValue = 4;

    private const int SearchResultReferenceTagValue = 19;

    /// <summary>Decodes <paramref name="message" /> as the response to the ExtendedRequest with <paramref name="messageId" />.</summary>
    /// <param name="message">One whole LDAPMessage.</param>
    /// <param name="messageId">The ExtendedRequest's messageID.</param>
    /// <returns>
    /// The answer and its result code, in the shape a bind's answer has;
    /// <see cref="LdapBindReplyStatus.SearchResult" /> for a search entry or reference with that
    /// messageID; and <see cref="LdapBindReplyStatus.Malformed" /> for any other message that is not
    /// a response to the request.
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

            Asn1Tag operation = ldapMessage.PeekTag();
            return operation.TagClass == TagClass.Application ? DecodeOperation(ldapMessage, operation) : Malformed;
        }
        catch (AsnContentException)
        {
            return Malformed;
        }
    }

    private static LdapBindReply Malformed => new(LdapBindReplyStatus.Malformed, 0);

    private static LdapBindReply DecodeOperation(AsnReader ldapMessage, Asn1Tag operation)
    {
        if (operation.TagValue is SearchResultEntryTagValue or SearchResultReferenceTagValue)
        {
            return new LdapBindReply(LdapBindReplyStatus.SearchResult, 0);
        }

        AsnReader response = ldapMessage.ReadSequence(operation);
        BigInteger resultCode = new(response.ReadEnumeratedBytes().Span, isUnsigned: false, isBigEndian: true);
        return resultCode < 0 || resultCode > int.MaxValue ? Malformed : new LdapBindReply(LdapBindReplyStatus.Answered, (int)resultCode);
    }
}
