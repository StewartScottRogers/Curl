using System.Formats.Asn1;
using System.Text;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Encodes the LDAP requests (RFC 4511 section 4) curl sends to bind, search, abandon and leave, each as
/// one whole LDAPMessage.
/// </summary>
internal static class LdapRequests
{
    /// <summary>The BindRequest's tag, <c>[APPLICATION 0]</c>.</summary>
    private static readonly Asn1Tag BindRequestTag = new(TagClass.Application, 0);

    /// <summary>The UnbindRequest's tag, <c>[APPLICATION 2]</c>, a primitive NULL.</summary>
    private static readonly Asn1Tag UnbindRequestTag = new(TagClass.Application, 2);

    /// <summary>The SearchRequest's tag, <c>[APPLICATION 3]</c>.</summary>
    private static readonly Asn1Tag SearchRequestTag = new(TagClass.Application, 3);

    /// <summary>The AbandonRequest's tag, <c>[APPLICATION 16]</c>, a primitive INTEGER.</summary>
    private static readonly Asn1Tag AbandonRequestTag = new(TagClass.Application, 16);

    /// <summary>The simple authentication choice's tag, <c>[0]</c>, a primitive OCTET STRING.</summary>
    private static readonly Asn1Tag SimpleAuthenticationTag = new(TagClass.ContextSpecific, 0);

    /// <summary>
    /// Encodes a BindRequest with simple authentication (RFC 4511 section 4.2): the anonymous
    /// bind when both <paramref name="name" /> and <paramref name="password" /> are empty.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="version">The protocol version, 3, or 2 for WinLDAP's retry.</param>
    /// <param name="name">The DN to bind as, sent as UTF-8.</param>
    /// <param name="password">The password, sent as UTF-8.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] Bind(LdapBerWriter writer, int messageId, int version, string name, string password) =>
        writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(messageId),
            writer.Constructed(
                BindRequestTag,
                LdapBerWriter.Integer(version),
                LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, Encoding.UTF8.GetBytes(name)),
                LdapBerWriter.OctetString(SimpleAuthenticationTag, Encoding.UTF8.GetBytes(password))));

    /// <summary>
    /// Encodes a SearchRequest (RFC 4511 section 4.5.1) as both builds send it:
    /// <c>neverDerefAliases</c>, no size or time limit, and <c>typesOnly</c> false.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="baseObject">The base DN's bytes.</param>
    /// <param name="scope">The scope, 0 to 3.</param>
    /// <param name="filter">The encoded Filter; WinLDAP may send several in a row.</param>
    /// <param name="attributes">Each attribute's bytes.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] Search(LdapBerWriter writer, int messageId, byte[] baseObject, int scope, byte[] filter, IEnumerable<byte[]> attributes) =>
        writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(messageId),
            writer.Constructed(
                SearchRequestTag,
                LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, baseObject),
                LdapBerWriter.Enumerated(scope),
                LdapBerWriter.Enumerated(0),
                LdapBerWriter.Integer(0),
                LdapBerWriter.Integer(0),
                LdapBerWriter.Boolean(false),
                filter,
                writer.Constructed(Asn1Tag.Sequence, [.. attributes.Select(attribute => LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, attribute))])));

    /// <summary>Encodes an AbandonRequest (RFC 4511 section 4.11), which <c>libldap</c> sends for a search it leaves unfinished.</summary>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="abandoned">The messageID of the request to abandon.</param>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] Abandon(LdapBerWriter writer, int messageId, int abandoned) =>
        writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(messageId),
            LdapBerWriter.Integer(abandoned, AbandonRequestTag));

    /// <summary>Encodes an UnbindRequest (RFC 4511 section 4.3).</summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] Unbind(LdapBerWriter writer, int messageId) =>
        writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(messageId),
            LdapBerWriter.Null(UnbindRequestTag));
}
