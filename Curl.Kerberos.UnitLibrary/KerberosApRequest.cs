using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// An AP-REQ (RFC 4120's <c>AP-REQ</c>, <c>[APPLICATION 14]</c>): a ticket, and an
/// authenticator (<see cref="KerberosAuthenticator" />) encrypted in the ticket's session key
/// that proves the client holds it. It is what a TGS-REQ's <c>PA-TGS-REQ</c> and a GSS-API
/// initial token carry.
/// </summary>
/// <param name="Options">The options.</param>
/// <param name="Ticket">The ticket.</param>
/// <param name="Authenticator">The encrypted authenticator: key usage 7 in a <c>PA-TGS-REQ</c>, 11 elsewhere.</param>
public sealed record KerberosApRequest(KerberosApOptions Options, KerberosTicket Ticket, KerberosEncryptedData Authenticator)
{
    private static readonly Func<AsnReader, KerberosApRequest> ContentsReader = new Func<AsnReader, KerberosApRequest>(ReadContents);

    /// <summary>Encodes the AP-REQ.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode((int)KerberosMessageType.ApRequest, writer =>
    {
        KerberosAsn1.WriteMessageHeader(writer, 0, KerberosMessageType.ApRequest);
        KerberosAsn1.WriteField(writer, 2, (uint)Options, KerberosAsn1.FlagsWriter);
        KerberosAsn1.WriteField(writer, 3, Ticket, KerberosTicket.Writer);
        KerberosAsn1.WriteField(writer, 4, Authenticator, KerberosEncryptedData.Writer);
    });

    /// <summary>Decodes an AP-REQ.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The AP-REQ.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosApRequest Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.Decode(bytes, (int)KerberosMessageType.ApRequest, ContentsReader);

    private static KerberosApRequest ReadContents(AsnReader reader)
    {
        KerberosAsn1.ReadMessageHeader(reader, 0, KerberosMessageType.ApRequest);
        return new KerberosApRequest(
            (KerberosApOptions)KerberosAsn1.ReadField(reader, 2, KerberosAsn1.FlagsReader),
            KerberosAsn1.ReadField(reader, 3, KerberosTicket.Reader),
            KerberosAsn1.ReadField(reader, 4, KerberosEncryptedData.Reader));
    }
}
