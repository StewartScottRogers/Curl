using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// A ticket (RFC 4120's <c>Ticket</c>, <c>[APPLICATION 1]</c>): the server it is for, and its
/// <c>EncTicketPart</c> encrypted in the server's key, which only the server reads.
/// </summary>
/// <param name="Realm">The server's realm, e.g. <c>EXAMPLE.TEST</c>.</param>
/// <param name="ServerName">The server's name, e.g. <c>krbtgt/EXAMPLE.TEST</c>.</param>
/// <param name="EncryptedPart">The encrypted <c>EncTicketPart</c>.</param>
public sealed record KerberosTicket(string Realm, KerberosPrincipalName ServerName, KerberosEncryptedData EncryptedPart)
{
    private const int ApplicationTag = 1;

    /// <summary>Reads a ticket.</summary>
    internal static readonly Func<AsnReader, KerberosTicket> Reader = new Func<AsnReader, KerberosTicket>(Read);

    /// <summary>Writes a ticket.</summary>
    internal static readonly Action<AsnWriter, KerberosTicket> Writer = new Action<AsnWriter, KerberosTicket>(Write);

    private static readonly Func<AsnReader, KerberosTicket> ContentsReader = new Func<AsnReader, KerberosTicket>(ReadContents);

    /// <summary>Encodes the ticket, e.g. to store it in a credential cache.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.EncodeValue(this, Writer);

    /// <summary>Decodes a ticket, e.g. from a credential cache (<see cref="CachedCredential.Ticket" />).</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The ticket.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosTicket Decode(ReadOnlyMemory<byte> bytes) => KerberosAsn1.DecodeValue(bytes, Reader);

    private static KerberosTicket Read(AsnReader reader) => KerberosAsn1.ReadApplication(reader, ApplicationTag, ContentsReader);

    private static void Write(AsnWriter writer, KerberosTicket ticket) => KerberosAsn1.WriteApplication(writer, ApplicationTag, inner =>
    {
        KerberosAsn1.WriteVersion(inner, 0);
        KerberosAsn1.WriteField(inner, 1, ticket.Realm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(inner, 2, ticket.ServerName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteField(inner, 3, ticket.EncryptedPart, KerberosEncryptedData.Writer);
    });

    private static KerberosTicket ReadContents(AsnReader reader)
    {
        KerberosAsn1.ReadVersion(reader, 0);
        return new KerberosTicket(
            KerberosAsn1.ReadField(reader, 1, KerberosAsn1.StringReader),
            KerberosAsn1.ReadField(reader, 2, KerberosPrincipalName.Reader),
            KerberosAsn1.ReadField(reader, 3, KerberosEncryptedData.Reader));
    }
}
