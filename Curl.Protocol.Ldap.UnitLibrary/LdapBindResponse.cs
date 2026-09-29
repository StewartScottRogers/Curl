using System.Formats.Asn1;
using System.Numerics;

namespace Curl.Protocol.Ldap;

/// <summary>Decodes a BindResponse (RFC 4511 section 4.2.2) with <see cref="AsnReader" />.</summary>
internal static class LdapBindResponse
{
    /// <summary>The BindResponse's tag, <c>[APPLICATION 1]</c>.</summary>
    private static readonly Asn1Tag BindResponseTag = new(TagClass.Application, 1, isConstructed: true);

    /// <summary>The LDAPResult's optional referral's tag, <c>[3]</c>.</summary>
    private static readonly Asn1Tag ReferralTag = new(TagClass.ContextSpecific, 3);

    /// <summary>The BindResponse's optional serverSaslCreds' tag, <c>[7]</c>, a primitive OCTET STRING.</summary>
    private static readonly Asn1Tag ServerSaslCredentialsTag = new(TagClass.ContextSpecific, 7);

    /// <summary>
    /// Decodes <paramref name="message" /> as the BindResponse to the request with
    /// <paramref name="messageId" />.
    /// </summary>
    /// <param name="message">One whole LDAPMessage, as <see cref="LdapMessageReader" /> reads it.</param>
    /// <param name="messageId">The messageID of the BindRequest it answers.</param>
    /// <returns>
    /// <see cref="LdapBindReplyStatus.Answered" /> with its resultCode, matchedDN and serverSaslCreds; or
    /// <see cref="LdapBindReplyStatus.Malformed" /> when the message is not valid BER, is not
    /// a BindResponse, answers another messageID, or carries a negative resultCode or one too
    /// large for an <see cref="int" />. The diagnosticMessage, the optional referral and
    /// controls are skipped.
    /// </returns>
    public static LdapBindReply Decode(ReadOnlyMemory<byte> message, int messageId)
    {
        try
        {
            AsnReader reader = new(message, AsnEncodingRules.BER);
            AsnReader ldapMessage = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            if (!ldapMessage.TryReadInt32(out int id) || id != messageId)
            {
                return Malformed;
            }

            AsnReader response = ldapMessage.ReadSequence(BindResponseTag);
            BigInteger resultCode = new(response.ReadEnumeratedBytes().Span, isUnsigned: false, isBigEndian: true);
            if (resultCode < 0 || resultCode > int.MaxValue)
            {
                return Malformed;
            }

            byte[] matchedDn = response.ReadOctetString();
            response.ReadOctetString();
            return new LdapBindReply(LdapBindReplyStatus.Answered, (int)resultCode)
            {
                MatchedDn = matchedDn,
                ServerSaslCredentials = ReadServerSaslCredentials(response),
            };
        }
        catch (AsnContentException)
        {
            return Malformed;
        }
    }

    /// <summary>Skips the optional referral and reads the optional serverSaslCreds after it; empty when there are none.</summary>
    private static byte[] ReadServerSaslCredentials(AsnReader response)
    {
        if (response.HasData && response.PeekTag().HasSameClassAndValue(ReferralTag))
        {
            response.ReadEncodedValue();
        }

        return response.HasData && response.PeekTag() == ServerSaslCredentialsTag
            ? response.ReadOctetString(ServerSaslCredentialsTag)
            : [];
    }

    private static LdapBindReply Malformed => new(LdapBindReplyStatus.Malformed, 0);
}
