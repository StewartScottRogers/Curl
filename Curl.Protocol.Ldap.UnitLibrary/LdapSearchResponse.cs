using System.Formats.Asn1;
using System.Numerics;
using System.Text;

namespace Curl.Protocol.Ldap;

/// <summary>Decodes the LDAPMessages a search is answered with (RFC 4511 section 4.5.2) with <see cref="AsnReader" />.</summary>
internal static class LdapSearchResponse
{
    /// <summary>The SearchResultEntry's tag, <c>[APPLICATION 4]</c>.</summary>
    private static readonly Asn1Tag SearchResultEntryTag = new(TagClass.Application, 4, isConstructed: true);

    /// <summary>The SearchResultDone's tag, <c>[APPLICATION 5]</c>.</summary>
    private static readonly Asn1Tag SearchResultDoneTag = new(TagClass.Application, 5, isConstructed: true);

    /// <summary>Decodes <paramref name="message" /> as a reply to the search with <paramref name="messageId" />.</summary>
    /// <param name="message">One whole LDAPMessage, as <see cref="LdapMessageReader" /> reads it.</param>
    /// <param name="messageId">The SearchRequest's messageID.</param>
    /// <returns>
    /// What the message is; <see cref="LdapSearchReplyKind.Lost" /> when it is not valid BER,
    /// has no messageID, or is a SearchResultDone whose resultCode is negative or too large for
    /// an <see cref="int" />. A SearchResultDone's referral and controls are skipped.
    /// </returns>
    public static LdapSearchReply Decode(ReadOnlyMemory<byte> message, int messageId)
    {
        try
        {
            AsnReader reader = new(message, AsnEncodingRules.BER);
            AsnReader ldapMessage = reader.ReadSequence();
            reader.ThrowIfNotEmpty();
            if (!ldapMessage.TryReadInt32(out int id))
            {
                return LdapSearchReply.Of(LdapSearchReplyKind.Lost);
            }

            if (id != messageId)
            {
                return LdapSearchReply.Of(LdapSearchReplyKind.OtherMessage);
            }

            Asn1Tag operation = ldapMessage.PeekTag();
            return operation == SearchResultDoneTag ? DecodeDone(ldapMessage)
                : LdapSearchReply.Of(operation == SearchResultEntryTag ? LdapSearchReplyKind.Entry : LdapSearchReplyKind.OtherResponse);
        }
        catch (AsnContentException)
        {
            return LdapSearchReply.Of(LdapSearchReplyKind.Lost);
        }
    }

    private static LdapSearchReply DecodeDone(AsnReader ldapMessage)
    {
        AsnReader done = ldapMessage.ReadSequence(SearchResultDoneTag);
        BigInteger resultCode = new(done.ReadEnumeratedBytes().Span, isUnsigned: false, isBigEndian: true);
        if (resultCode < 0 || resultCode > int.MaxValue)
        {
            return LdapSearchReply.Of(LdapSearchReplyKind.Lost);
        }

        done.ReadOctetString();
        string diagnosticMessage = Encoding.UTF8.GetString(done.ReadOctetString());
        return new LdapSearchReply(LdapSearchReplyKind.Done, (int)resultCode, diagnosticMessage);
    }
}
