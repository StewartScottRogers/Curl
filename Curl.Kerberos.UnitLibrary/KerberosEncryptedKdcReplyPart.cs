using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// The part of an AS-REP or TGS-REP only the client can decrypt (RFC 4120's
/// <c>EncKDCRepPart</c>, <c>EncASRepPart</c> <c>[APPLICATION 25]</c> or <c>EncTGSRepPart</c>
/// <c>[APPLICATION 26]</c>): the session key that goes with the ticket, and what the KDC
/// says about the ticket. <see cref="Dispose" /> zeroes the session key.
/// </summary>
public sealed class KerberosEncryptedKdcReplyPart : IDisposable
{
    private const int AsReplyTag = 25;
    private const int TgsReplyTag = 26;

    private static readonly Func<AsnReader, KerberosEncryptedKdcReplyPart> TaggedReader = new Func<AsnReader, KerberosEncryptedKdcReplyPart>(ReadTagged);

    /// <summary>
    /// Gets which reply's part this is by its application tag: <see cref="KerberosMessageType.AsReply" />
    /// for <c>[APPLICATION 25]</c>, <see cref="KerberosMessageType.TgsReply" /> for <c>[APPLICATION 26]</c>.
    /// Some KDCs put tag 26 inside an AS-REP too, so the tag read is kept rather than checked
    /// against the reply, and <see cref="Encode" /> writes it back.
    /// </summary>
    public required KerberosMessageType ReplyType { get; init; }

    /// <summary>Gets the session key that goes with the ticket.</summary>
    public required KerberosKey Key { get; init; }

    /// <summary>Gets when the client last did things the KDC reports.</summary>
    public required IReadOnlyList<KerberosLastRequest> LastRequests { get; init; }

    /// <summary>Gets the nonce, which must equal the request's.</summary>
    public required uint Nonce { get; init; }

    /// <summary>Gets when the client's key expires; <see langword="null" /> when the KDC does not say.</summary>
    public DateTimeOffset? KeyExpiration { get; init; }

    /// <summary>Gets the ticket's flags.</summary>
    public required KerberosTicketFlags Flags { get; init; }

    /// <summary>Gets when the client first authenticated.</summary>
    public required DateTimeOffset AuthenticationTime { get; init; }

    /// <summary>Gets when the ticket becomes valid; <see langword="null" /> when it is the authentication time.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Gets when the ticket expires.</summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Gets the last time the ticket can be renewed to; <see langword="null" /> when it is not renewable.</summary>
    public DateTimeOffset? RenewUntil { get; init; }

    /// <summary>Gets the server's realm.</summary>
    public required string ServerRealm { get; init; }

    /// <summary>Gets the server's name.</summary>
    public required KerberosPrincipalName ServerName { get; init; }

    /// <summary>Gets the addresses the ticket is bound to; empty for an addressless ticket.</summary>
    public IReadOnlyList<KerberosAddress> ClientAddresses { get; init; } = [];

    /// <summary>Gets the encrypted pre-authentication data (RFC 6806's <c>encrypted-pa-data</c>); empty when there is none.</summary>
    public IReadOnlyList<KerberosPreAuthenticationData> EncryptedPreAuthenticationData { get; init; } = [];

    /// <summary>Encodes the part, ready to encrypt. The bytes hold the session key; the caller zeroes them.</summary>
    /// <returns>The DER encoding.</returns>
    /// <exception cref="InvalidOperationException"><see cref="ReplyType" /> is neither reply.</exception>
    public byte[] Encode() => KerberosAsn1.Encode(
        ReplyType switch
        {
            KerberosMessageType.AsReply => AsReplyTag,
            KerberosMessageType.TgsReply => TgsReplyTag,
            _ => throw new InvalidOperationException($"An encrypted KDC reply part belongs to an AS-REP or TGS-REP, not {ReplyType}."),
        },
        WriteContents);

    /// <summary>Decodes a part decrypted from an AS-REP or TGS-REP.</summary>
    /// <param name="bytes">The plaintext.</param>
    /// <returns>The part; the caller disposes it.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" /> or <see cref="KerberosMessageError.UnexpectedMessage" />.
    /// </exception>
    public static KerberosEncryptedKdcReplyPart Decode(ReadOnlyMemory<byte> bytes) => KerberosAsn1.DecodeValue(bytes, TaggedReader);

    /// <summary>Zeroes the session key.</summary>
    public void Dispose() => Key.Dispose();

    private static KerberosEncryptedKdcReplyPart ReadTagged(AsnReader reader)
    {
        int tag = reader.PeekTag().HasSameClassAndValue(KerberosAsn1.Application(AsReplyTag)) ? AsReplyTag : TgsReplyTag;
        KerberosMessageType replyType = tag == AsReplyTag ? KerberosMessageType.AsReply : KerberosMessageType.TgsReply;
        return KerberosAsn1.ReadApplication(reader, tag, inner => ReadContents(inner, replyType));
    }

    private static KerberosEncryptedKdcReplyPart ReadContents(AsnReader reader, KerberosMessageType replyType)
    {
        KerberosKey key = KerberosAsn1.ReadField(reader, 0, KerberosAsn1.KeyReader);
        return KerberosAsn1.DisposeKeyOnFailure(key, () => ReadAfterKey(reader, replyType, key));
    }

    private static KerberosEncryptedKdcReplyPart ReadAfterKey(AsnReader reader, KerberosMessageType replyType, KerberosKey key) => new()
    {
        ReplyType = replyType,
        Key = key,
        LastRequests = KerberosAsn1.ReadSequenceOfField(reader, 1, KerberosLastRequest.Reader),
        Nonce = KerberosAsn1.ReadField(reader, 2, KerberosAsn1.UInt32Reader),
        KeyExpiration = KerberosAsn1.ReadOptionalValueField(reader, 3, KerberosAsn1.TimeReader),
        Flags = (KerberosTicketFlags)KerberosAsn1.ReadField(reader, 4, KerberosAsn1.FlagsReader),
        AuthenticationTime = KerberosAsn1.ReadField(reader, 5, KerberosAsn1.TimeReader),
        StartTime = KerberosAsn1.ReadOptionalValueField(reader, 6, KerberosAsn1.TimeReader),
        EndTime = KerberosAsn1.ReadField(reader, 7, KerberosAsn1.TimeReader),
        RenewUntil = KerberosAsn1.ReadOptionalValueField(reader, 8, KerberosAsn1.TimeReader),
        ServerRealm = KerberosAsn1.ReadField(reader, 9, KerberosAsn1.StringReader),
        ServerName = KerberosAsn1.ReadField(reader, 10, KerberosPrincipalName.Reader),
        ClientAddresses = KerberosAsn1.ReadOptionalSequenceOfField(reader, 11, KerberosAsn1.AddressReader),
        EncryptedPreAuthenticationData = KerberosAsn1.ReadOptionalSequenceOfField(reader, 12, KerberosPreAuthenticationData.Reader),
    };

    private void WriteContents(AsnWriter writer)
    {
        KerberosAsn1.WriteField(writer, 0, Key, KerberosAsn1.KeyWriter);
        KerberosAsn1.WriteSequenceOfField(writer, 1, LastRequests, KerberosLastRequest.Writer);
        KerberosAsn1.WriteField(writer, 2, Nonce, KerberosAsn1.UInt32Writer);
        KerberosAsn1.WriteOptionalValueField(writer, 3, KeyExpiration, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(writer, 4, (uint)Flags, KerberosAsn1.FlagsWriter);
        KerberosAsn1.WriteField(writer, 5, AuthenticationTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(writer, 6, StartTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(writer, 7, EndTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(writer, 8, RenewUntil, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(writer, 9, ServerRealm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(writer, 10, ServerName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteOptionalSequenceOfField(writer, 11, ClientAddresses, KerberosAsn1.AddressWriter);
        KerberosAsn1.WriteOptionalSequenceOfField(writer, 12, EncryptedPreAuthenticationData, KerberosPreAuthenticationData.Writer);
    }
}
