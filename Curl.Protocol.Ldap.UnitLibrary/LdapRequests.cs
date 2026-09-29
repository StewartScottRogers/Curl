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

    /// <summary>The SASL authentication choice's tag, <c>[3]</c>, a constructed SaslCredentials.</summary>
    private static readonly Asn1Tag SaslAuthenticationTag = new(TagClass.ContextSpecific, 3);

    /// <summary>The present filter's tag, <c>[7]</c>, a primitive AttributeDescription.</summary>
    private static readonly Asn1Tag PresentFilterTag = new(TagClass.ContextSpecific, 7);

    /// <summary>The time limit, in seconds, WinLDAP puts on its rootDSE reads.</summary>
    private const int RootDseTimeLimit = 120;

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
        BindWith(writer, messageId, version, name, LdapBerWriter.OctetString(SimpleAuthenticationTag, Encoding.UTF8.GetBytes(password)));

    /// <summary>
    /// Encodes an LDAPv3 BindRequest with SASL authentication (RFC 4511 section 4.2) and an
    /// empty name, as WinLDAP sends its <c>GSS-SPNEGO</c> logon bind.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="mechanism">The SASL mechanism's name.</param>
    /// <param name="credentials">The mechanism's token.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] SaslBind(LdapBerWriter writer, int messageId, string mechanism, ReadOnlySpan<byte> credentials) =>
        BindWith(
            writer,
            messageId,
            3,
            string.Empty,
            writer.Constructed(
                SaslAuthenticationTag,
                LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, Encoding.UTF8.GetBytes(mechanism)),
                LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, credentials)));

    /// <summary>
    /// Encodes an LDAPv3 BindRequest with one of Microsoft's Sicily authentication choices
    /// (MS-ADTS 5.1.1.1.3), as WinLDAP sends its NTLM logon bind to a server that offers no
    /// <c>GSS-SPNEGO</c>: <c>[10]</c> <c>sicilyNegotiate</c> with the name <c>NTLM</c>, then
    /// <c>[11]</c> <c>sicilyResponse</c> with an empty name.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="name">The BindRequest's name.</param>
    /// <param name="choice">The authentication choice's tag number, 10 or 11.</param>
    /// <param name="token">The NTLM message.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] SicilyBind(LdapBerWriter writer, int messageId, string name, int choice, ReadOnlySpan<byte> token) =>
        BindWith(writer, messageId, 3, name, LdapBerWriter.OctetString(new Asn1Tag(TagClass.ContextSpecific, choice), token));

    /// <summary>
    /// Encodes a SearchRequest for one attribute of the rootDSE as WinLDAP sends it before its
    /// logon bind: base <c>""</c>, scope <c>baseObject</c>, time limit 120, filter
    /// <c>(objectclass=*)</c>.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="attribute">The attribute to read.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] RootDseSearch(LdapBerWriter writer, int messageId, string attribute) =>
        Search(
            writer,
            messageId,
            [],
            0,
            LdapBerWriter.OctetString(PresentFilterTag, "objectclass"u8),
            [Encoding.UTF8.GetBytes(attribute)],
            RootDseTimeLimit);

    /// <summary>Encodes a BindRequest with <paramref name="authentication" /> as its AuthenticationChoice.</summary>
    private static byte[] BindWith(LdapBerWriter writer, int messageId, int version, string name, byte[] authentication) =>
        writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(messageId),
            writer.Constructed(
                BindRequestTag,
                LdapBerWriter.Integer(version),
                LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, Encoding.UTF8.GetBytes(name)),
                authentication));

    /// <summary>
    /// Encodes a SearchRequest (RFC 4511 section 4.5.1) as both builds send it:
    /// <c>neverDerefAliases</c>, no size limit, the time limit given, and <c>typesOnly</c> false.
    /// </summary>
    /// <param name="writer">Writes the elements with the dialect's length form.</param>
    /// <param name="messageId">The LDAPMessage's messageID.</param>
    /// <param name="baseObject">The base DN's bytes.</param>
    /// <param name="scope">The scope, 0 to 3.</param>
    /// <param name="filter">The encoded Filter; WinLDAP may send several in a row.</param>
    /// <param name="attributes">Each attribute's bytes.</param>
    /// <param name="timeLimit">The time limit in seconds; 0, none, for the search the URL names.</param>
    /// <returns>The LDAPMessage.</returns>
    public static byte[] Search(LdapBerWriter writer, int messageId, byte[] baseObject, int scope, byte[] filter, IEnumerable<byte[]> attributes, int timeLimit = 0) =>
        writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(messageId),
            writer.Constructed(
                SearchRequestTag,
                LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, baseObject),
                LdapBerWriter.Enumerated(scope),
                LdapBerWriter.Enumerated(0),
                LdapBerWriter.Integer(0),
                LdapBerWriter.Integer(timeLimit),
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
