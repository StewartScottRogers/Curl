using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// An AS-REQ or TGS-REQ (RFC 4120's <c>KDC-REQ</c>, <c>[APPLICATION 10]</c> and
/// <c>[APPLICATION 12]</c>): pre-authentication, and the body saying what is asked for.
/// </summary>
public sealed class KerberosKdcRequest
{
    private static readonly Func<AsnReader, KerberosMessageType, KerberosKdcRequest> ContentsReader = new Func<AsnReader, KerberosMessageType, KerberosKdcRequest>(ReadContents);

    /// <summary>Gets which request this is: <see cref="KerberosMessageType.AsRequest" /> or <see cref="KerberosMessageType.TgsRequest" />.</summary>
    public required KerberosMessageType MessageType { get; init; }

    /// <summary>Gets the pre-authentication data, e.g. <c>PA-ENC-TIMESTAMP</c> or <c>PA-TGS-REQ</c>; empty when there is none.</summary>
    public IReadOnlyList<KerberosPreAuthenticationData> PreAuthenticationData { get; init; } = [];

    /// <summary>Gets what is asked for.</summary>
    public required KerberosKdcRequestBody Body { get; init; }

    /// <summary>Encodes the request.</summary>
    /// <returns>The DER encoding.</returns>
    /// <exception cref="InvalidOperationException"><see cref="MessageType" /> is neither request.</exception>
    public byte[] Encode() => KerberosAsn1.Encode(KerberosMessage.ApplicationTagOf(MessageType, KerberosMessageType.AsRequest, KerberosMessageType.TgsRequest), writer =>
    {
        KerberosAsn1.WriteMessageHeader(writer, 1, MessageType);
        KerberosAsn1.WriteOptionalSequenceOfField(writer, 3, PreAuthenticationData, KerberosPreAuthenticationData.Writer);
        KerberosAsn1.WriteField(writer, 4, Body, KerberosKdcRequestBody.Writer);
    });

    /// <summary>Decodes an AS-REQ or a TGS-REQ.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The request.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosKdcRequest Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosMessage.DecodeEither(bytes, KerberosMessageType.AsRequest, KerberosMessageType.TgsRequest, ContentsReader);

    private static KerberosKdcRequest ReadContents(AsnReader reader, KerberosMessageType messageType)
    {
        KerberosAsn1.ReadMessageHeader(reader, 1, messageType);
        return new KerberosKdcRequest
        {
            MessageType = messageType,
            PreAuthenticationData = KerberosAsn1.ReadOptionalSequenceOfField(reader, 3, KerberosPreAuthenticationData.Reader),
            Body = KerberosAsn1.ReadField(reader, 4, KerberosKdcRequestBody.Reader),
        };
    }
}
