using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// What an AS-REQ or TGS-REQ asks for (RFC 4120's <c>KDC-REQ-BODY</c>). A TGS-REQ's
/// <c>PA-TGS-REQ</c> authenticator checksums these exact bytes, so <see cref="Encode" />
/// gives them alone.
/// </summary>
public sealed class KerberosKdcRequestBody
{
    /// <summary>Reads a <c>KDC-REQ-BODY</c>.</summary>
    internal static readonly Func<AsnReader, KerberosKdcRequestBody> Reader = new Func<AsnReader, KerberosKdcRequestBody>(Read);

    /// <summary>Writes a <c>KDC-REQ-BODY</c>.</summary>
    internal static readonly Action<AsnWriter, KerberosKdcRequestBody> Writer = new Action<AsnWriter, KerberosKdcRequestBody>(Write);

    private static readonly Func<AsnReader, KerberosKdcRequestBody> ContentsReader = new Func<AsnReader, KerberosKdcRequestBody>(ReadContents);

    /// <summary>Gets what the client asks for.</summary>
    public required KerberosKdcOptions Options { get; init; }

    /// <summary>Gets the client's name; <see langword="null" /> in a TGS-REQ, where the ticket names the client.</summary>
    public KerberosPrincipalName? ClientName { get; init; }

    /// <summary>Gets the realm: the client's in an AS-REQ, the server's in a TGS-REQ.</summary>
    public required string Realm { get; init; }

    /// <summary>Gets the server's name; <see langword="null" /> only with <c>enc-tkt-in-skey</c>.</summary>
    public KerberosPrincipalName? ServerName { get; init; }

    /// <summary>Gets the start time of a postdated ticket; <see langword="null" /> for one valid now.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Gets the end time asked for.</summary>
    public required DateTimeOffset Till { get; init; }

    /// <summary>Gets the renew-until time asked for; <see langword="null" /> for a ticket that is not renewable.</summary>
    public DateTimeOffset? RenewTill { get; init; }

    /// <summary>Gets the nonce the reply's encrypted part must repeat.</summary>
    public required uint Nonce { get; init; }

    /// <summary>Gets the encryption types the client accepts, in its order of preference.</summary>
    public required IReadOnlyList<int> EncryptionTypes { get; init; }

    /// <summary>Gets the addresses the ticket is to be bound to; empty for an addressless ticket.</summary>
    public IReadOnlyList<KerberosAddress> Addresses { get; init; } = [];

    /// <summary>Gets authorization data for the ticket, encrypted; <see langword="null" /> when there is none.</summary>
    public KerberosEncryptedData? EncryptedAuthorizationData { get; init; }

    /// <summary>Gets the additional tickets, e.g. the second ticket of a user-to-user TGS-REQ.</summary>
    public IReadOnlyList<KerberosTicket> AdditionalTickets { get; init; } = [];

    /// <summary>Encodes the <c>KDC-REQ-BODY</c> alone, as a TGS-REQ authenticator's checksum covers it.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.EncodeValue(this, Writer);

    private static KerberosKdcRequestBody Read(AsnReader reader) => KerberosAsn1.ReadSequence(reader, ContentsReader);

    private static KerberosKdcRequestBody ReadContents(AsnReader reader) => new()
    {
        Options = (KerberosKdcOptions)KerberosAsn1.ReadField(reader, 0, KerberosAsn1.FlagsReader),
        ClientName = KerberosAsn1.ReadOptionalField(reader, 1, KerberosPrincipalName.Reader),
        Realm = KerberosAsn1.ReadField(reader, 2, KerberosAsn1.StringReader),
        ServerName = KerberosAsn1.ReadOptionalField(reader, 3, KerberosPrincipalName.Reader),
        From = KerberosAsn1.ReadOptionalValueField(reader, 4, KerberosAsn1.TimeReader),
        Till = KerberosAsn1.ReadField(reader, 5, KerberosAsn1.TimeReader),
        RenewTill = KerberosAsn1.ReadOptionalValueField(reader, 6, KerberosAsn1.TimeReader),
        Nonce = KerberosAsn1.ReadField(reader, 7, KerberosAsn1.UInt32Reader),
        EncryptionTypes = KerberosAsn1.ReadSequenceOfField(reader, 8, KerberosAsn1.Int32Reader),
        Addresses = KerberosAsn1.ReadOptionalSequenceOfField(reader, 9, KerberosAsn1.AddressReader),
        EncryptedAuthorizationData = KerberosAsn1.ReadOptionalField(reader, 10, KerberosEncryptedData.Reader),
        AdditionalTickets = KerberosAsn1.ReadOptionalSequenceOfField(reader, 11, KerberosTicket.Reader),
    };

    private static void Write(AsnWriter writer, KerberosKdcRequestBody body) => KerberosAsn1.WriteSequence(writer, inner =>
    {
        KerberosAsn1.WriteField(inner, 0, (uint)body.Options, KerberosAsn1.FlagsWriter);
        KerberosAsn1.WriteOptionalField(inner, 1, body.ClientName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteField(inner, 2, body.Realm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteOptionalField(inner, 3, body.ServerName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteOptionalValueField(inner, 4, body.From, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(inner, 5, body.Till, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(inner, 6, body.RenewTill, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(inner, 7, body.Nonce, KerberosAsn1.UInt32Writer);
        KerberosAsn1.WriteSequenceOfField(inner, 8, body.EncryptionTypes, KerberosAsn1.Int32Writer);
        KerberosAsn1.WriteOptionalSequenceOfField(inner, 9, body.Addresses, KerberosAsn1.AddressWriter);
        KerberosAsn1.WriteOptionalField(inner, 10, body.EncryptedAuthorizationData, KerberosEncryptedData.Writer);
        KerberosAsn1.WriteOptionalSequenceOfField(inner, 11, body.AdditionalTickets, KerberosTicket.Writer);
    });
}
