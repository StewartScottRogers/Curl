using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// An AP-REP (RFC 4120's <c>AP-REP</c>, <c>[APPLICATION 15]</c>): the server's answer to a
/// mutual-authentication AP-REQ, holding a <see cref="KerberosEncryptedApReplyPart" />.
/// </summary>
/// <param name="EncryptedPart">The encrypted <see cref="KerberosEncryptedApReplyPart" />, key usage 12, in the ticket's session key.</param>
public sealed record KerberosApReply(KerberosEncryptedData EncryptedPart)
{
    private static readonly Func<AsnReader, KerberosApReply> ContentsReader = new Func<AsnReader, KerberosApReply>(ReadContents);

    /// <summary>Encodes the AP-REP.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode((int)KerberosMessageType.ApReply, writer =>
    {
        KerberosAsn1.WriteMessageHeader(writer, 0, KerberosMessageType.ApReply);
        KerberosAsn1.WriteField(writer, 2, EncryptedPart, KerberosEncryptedData.Writer);
    });

    /// <summary>Decodes an AP-REP.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The AP-REP.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosApReply Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.Decode(bytes, (int)KerberosMessageType.ApReply, ContentsReader);

    private static KerberosApReply ReadContents(AsnReader reader)
    {
        KerberosAsn1.ReadMessageHeader(reader, 0, KerberosMessageType.ApReply);
        return new KerberosApReply(KerberosAsn1.ReadField(reader, 2, KerberosEncryptedData.Reader));
    }
}
