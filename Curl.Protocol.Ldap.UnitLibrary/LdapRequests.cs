using System.Formats.Asn1;
using System.Text;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Encodes the LDAP requests (RFC 4511 section 4) curl sends to bind and to leave, each as
/// one whole LDAPMessage.
/// </summary>
internal static class LdapRequests
{
    /// <summary>The BindRequest's tag, <c>[APPLICATION 0]</c>.</summary>
    private static readonly Asn1Tag BindRequestTag = new(TagClass.Application, 0);

    /// <summary>The UnbindRequest's tag, <c>[APPLICATION 2]</c>, a primitive NULL.</summary>
    private static readonly Asn1Tag UnbindRequestTag = new(TagClass.Application, 2);

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
