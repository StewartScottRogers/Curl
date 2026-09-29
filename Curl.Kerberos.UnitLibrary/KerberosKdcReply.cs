using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// An AS-REP or TGS-REP (RFC 4120's <c>KDC-REP</c>, <c>[APPLICATION 11]</c> and
/// <c>[APPLICATION 13]</c>): the ticket issued, and the part only the client can decrypt
/// (<see cref="KerberosEncryptedKdcReplyPart" />) holding its session key.
/// </summary>
public sealed class KerberosKdcReply
{
    private static readonly Func<AsnReader, KerberosMessageType, KerberosKdcReply> ContentsReader = new Func<AsnReader, KerberosMessageType, KerberosKdcReply>(ReadContents);

    /// <summary>Gets which reply this is: <see cref="KerberosMessageType.AsReply" /> or <see cref="KerberosMessageType.TgsReply" />.</summary>
    public required KerberosMessageType MessageType { get; init; }

    /// <summary>Gets the pre-authentication data, e.g. <c>PA-ETYPE-INFO2</c>; empty when there is none.</summary>
    public IReadOnlyList<KerberosPreAuthenticationData> PreAuthenticationData { get; init; } = [];

    /// <summary>Gets the client's realm.</summary>
    public required string ClientRealm { get; init; }

    /// <summary>Gets the client's name.</summary>
    public required KerberosPrincipalName ClientName { get; init; }

    /// <summary>Gets the ticket issued.</summary>
    public required KerberosTicket Ticket { get; init; }

    /// <summary>
    /// Gets the encrypted <see cref="KerberosEncryptedKdcReplyPart" />: in the client's key
    /// (key usage 3) for an AS-REP, in the TGS session key or subkey (8 or 9) for a TGS-REP.
    /// </summary>
    public required KerberosEncryptedData EncryptedPart { get; init; }

    /// <summary>Encodes the reply.</summary>
    /// <returns>The DER encoding.</returns>
    /// <exception cref="InvalidOperationException"><see cref="MessageType" /> is neither reply.</exception>
    public byte[] Encode() => KerberosAsn1.Encode(KerberosMessage.ApplicationTagOf(MessageType, KerberosMessageType.AsReply, KerberosMessageType.TgsReply), writer =>
    {
        KerberosAsn1.WriteMessageHeader(writer, 0, MessageType);
        KerberosAsn1.WriteOptionalSequenceOfField(writer, 2, PreAuthenticationData, KerberosPreAuthenticationData.Writer);
        KerberosAsn1.WriteField(writer, 3, ClientRealm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(writer, 4, ClientName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteField(writer, 5, Ticket, KerberosTicket.Writer);
        KerberosAsn1.WriteField(writer, 6, EncryptedPart, KerberosEncryptedData.Writer);
    });

    /// <summary>Decodes an AS-REP or a TGS-REP.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The reply.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosKdcReply Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosMessage.DecodeEither(bytes, KerberosMessageType.AsReply, KerberosMessageType.TgsReply, ContentsReader);

    private static KerberosKdcReply ReadContents(AsnReader reader, KerberosMessageType messageType)
    {
        KerberosAsn1.ReadMessageHeader(reader, 0, messageType);
        return new KerberosKdcReply
        {
            MessageType = messageType,
            PreAuthenticationData = KerberosAsn1.ReadOptionalSequenceOfField(reader, 2, KerberosPreAuthenticationData.Reader),
            ClientRealm = KerberosAsn1.ReadField(reader, 3, KerberosAsn1.StringReader),
            ClientName = KerberosAsn1.ReadField(reader, 4, KerberosPrincipalName.Reader),
            Ticket = KerberosAsn1.ReadField(reader, 5, KerberosTicket.Reader),
            EncryptedPart = KerberosAsn1.ReadField(reader, 6, KerberosEncryptedData.Reader),
        };
    }
}
