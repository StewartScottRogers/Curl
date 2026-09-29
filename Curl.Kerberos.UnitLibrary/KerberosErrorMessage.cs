using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// A KRB-ERROR (RFC 4120's <c>KRB-ERROR</c>, <c>[APPLICATION 30]</c>): a KDC's or server's
/// refusal, with its error code, and for <see cref="PreAuthenticationRequired" /> the
/// <c>METHOD-DATA</c> in <see cref="ErrorData" /> saying how to pre-authenticate.
/// </summary>
public sealed class KerberosErrorMessage
{
    /// <summary><c>KDC_ERR_PREAUTH_REQUIRED</c>: the client must pre-authenticate; <see cref="ErrorData" /> holds <c>METHOD-DATA</c>.</summary>
    public const int PreAuthenticationRequired = 25;

    /// <summary><c>KRB_ERR_RESPONSE_TOO_BIG</c>: the reply does not fit a UDP datagram; ask again over TCP (RFC 4120 section 7.2.1).</summary>
    public const int ResponseTooBig = 52;

    private static readonly Func<AsnReader, KerberosErrorMessage> ContentsReader = new Func<AsnReader, KerberosErrorMessage>(ReadContents);

    /// <summary>Gets the client's time from the request; <see langword="null" /> when the request gave none.</summary>
    public DateTimeOffset? ClientTime { get; init; }

    /// <summary>Gets the microseconds past <see cref="ClientTime" />; <see langword="null" /> when not given.</summary>
    public int? ClientMicroseconds { get; init; }

    /// <summary>Gets the server's time, to the second.</summary>
    public required DateTimeOffset ServerTime { get; init; }

    /// <summary>Gets the microseconds past <see cref="ServerTime" />.</summary>
    public required int ServerMicroseconds { get; init; }

    /// <summary>Gets the error code (RFC 4120 section 7.5.9), e.g. <see cref="PreAuthenticationRequired" />.</summary>
    public required int ErrorCode { get; init; }

    /// <summary>Gets the client's realm; <see langword="null" /> when not given.</summary>
    public string? ClientRealm { get; init; }

    /// <summary>Gets the client's name; <see langword="null" /> when not given.</summary>
    public KerberosPrincipalName? ClientName { get; init; }

    /// <summary>Gets the server's realm.</summary>
    public required string Realm { get; init; }

    /// <summary>Gets the server's name.</summary>
    public required KerberosPrincipalName ServerName { get; init; }

    /// <summary>Gets the error's text, e.g. MIT's <c>NEEDED_PREAUTH</c>; <see langword="null" /> when not given.</summary>
    public string? ErrorText { get; init; }

    /// <summary>
    /// Gets the error data, e.g. <c>METHOD-DATA</c> that
    /// <see cref="KerberosPreAuthenticationData.DecodeMethodData" /> reads; <see langword="null" /> when not given.
    /// </summary>
    public byte[]? ErrorData { get; init; }

    /// <summary>Encodes the KRB-ERROR.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode((int)KerberosMessageType.Error, writer =>
    {
        KerberosAsn1.WriteMessageHeader(writer, 0, KerberosMessageType.Error);
        KerberosAsn1.WriteOptionalValueField(writer, 2, ClientTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(writer, 3, ClientMicroseconds, KerberosAsn1.Int32Writer);
        KerberosAsn1.WriteField(writer, 4, ServerTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(writer, 5, ServerMicroseconds, KerberosAsn1.Int32Writer);
        KerberosAsn1.WriteField(writer, 6, ErrorCode, KerberosAsn1.Int32Writer);
        KerberosAsn1.WriteOptionalField(writer, 7, ClientRealm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteOptionalField(writer, 8, ClientName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteField(writer, 9, Realm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(writer, 10, ServerName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteOptionalField(writer, 11, ErrorText, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteOptionalField(writer, 12, ErrorData, KerberosAsn1.OctetsWriter);
    });

    /// <summary>Decodes a KRB-ERROR.</summary>
    /// <param name="bytes">The encoding.</param>
    /// <returns>The error.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosErrorMessage Decode(ReadOnlyMemory<byte> bytes) =>
        KerberosAsn1.Decode(bytes, (int)KerberosMessageType.Error, ContentsReader);

    private static KerberosErrorMessage ReadContents(AsnReader reader)
    {
        KerberosAsn1.ReadMessageHeader(reader, 0, KerberosMessageType.Error);
        return new KerberosErrorMessage
        {
            ClientTime = KerberosAsn1.ReadOptionalValueField(reader, 2, KerberosAsn1.TimeReader),
            ClientMicroseconds = KerberosAsn1.ReadOptionalValueField(reader, 3, KerberosAsn1.Int32Reader),
            ServerTime = KerberosAsn1.ReadField(reader, 4, KerberosAsn1.TimeReader),
            ServerMicroseconds = KerberosAsn1.ReadField(reader, 5, KerberosAsn1.Int32Reader),
            ErrorCode = KerberosAsn1.ReadField(reader, 6, KerberosAsn1.Int32Reader),
            ClientRealm = KerberosAsn1.ReadOptionalField(reader, 7, KerberosAsn1.StringReader),
            ClientName = KerberosAsn1.ReadOptionalField(reader, 8, KerberosPrincipalName.Reader),
            Realm = KerberosAsn1.ReadField(reader, 9, KerberosAsn1.StringReader),
            ServerName = KerberosAsn1.ReadField(reader, 10, KerberosPrincipalName.Reader),
            ErrorText = KerberosAsn1.ReadOptionalField(reader, 11, KerberosAsn1.StringReader),
            ErrorData = KerberosAsn1.ReadOptionalField(reader, 12, KerberosAsn1.OctetsReader),
        };
    }
}
