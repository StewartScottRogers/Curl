using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// A KRB-CRED (RFC 4120's <c>KRB-CRED</c>, <c>[APPLICATION 22]</c>): tickets forwarded to
/// another party, with their keys in a <see cref="KerberosEncryptedCredentialPart" />. It is
/// what RFC 4121's delegation carries in the authenticator checksum.
/// </summary>
/// <param name="Tickets">The tickets.</param>
/// <param name="EncryptedPart">The encrypted <see cref="KerberosEncryptedCredentialPart" />, key usage 14.</param>
public sealed record KerberosCredentialMessage(IReadOnlyList<KerberosTicket> Tickets, KerberosEncryptedData EncryptedPart)
{
    private static readonly Func<AsnReader, KerberosCredentialMessage> ContentsReader = new Func<AsnReader, KerberosCredentialMessage>(ReadContents);

    /// <summary>Encodes the KRB-CRED.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode((int)KerberosMessageType.Credential, writer =>
    {
        KerberosAsn1.WriteMessageHeader(writer, 0, KerberosMessageType.Credential);
        KerberosAsn1.WriteSequenceOfField(writer, 2, Tickets, KerberosTicket.Writer);
        KerberosAsn1.WriteField(writer, 3, EncryptedPart, KerberosEncryptedData.Writer);
    });

    /// <summary>Decodes a KRB-CRED.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The KRB-CRED.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosCredentialMessage Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.Decode(bytes, (int)KerberosMessageType.Credential, ContentsReader);

    private static KerberosCredentialMessage ReadContents(AsnReader reader)
    {
        KerberosAsn1.ReadMessageHeader(reader, 0, KerberosMessageType.Credential);
        return new KerberosCredentialMessage(
            KerberosAsn1.ReadSequenceOfField(reader, 2, KerberosTicket.Reader),
            KerberosAsn1.ReadField(reader, 3, KerberosEncryptedData.Reader));
    }
}
