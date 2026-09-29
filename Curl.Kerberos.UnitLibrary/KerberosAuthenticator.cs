using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// The plaintext of an AP-REQ's authenticator (RFC 4120's <c>Authenticator</c>,
/// <c>[APPLICATION 2]</c>): who the client is and when it spoke, with an optional checksum,
/// subkey and sequence number. <see cref="Dispose" /> zeroes the subkey.
/// </summary>
public sealed class KerberosAuthenticator : IDisposable
{
    private const int ApplicationTag = 2;

    private static readonly Func<AsnReader, KerberosAuthenticator> ContentsReader = new Func<AsnReader, KerberosAuthenticator>(ReadContents);

    /// <summary>Gets the client's realm.</summary>
    public required string ClientRealm { get; init; }

    /// <summary>Gets the client's name.</summary>
    public required KerberosPrincipalName ClientName { get; init; }

    /// <summary>
    /// Gets the checksum: of the TGS-REQ's body in a <c>PA-TGS-REQ</c>, RFC 4121's GSS-API
    /// checksum in a GSS-API token; <see langword="null" /> when there is none.
    /// </summary>
    public KerberosChecksum? Checksum { get; init; }

    /// <summary>Gets the microseconds past <see cref="ClientTime" />.</summary>
    public required int ClientMicroseconds { get; init; }

    /// <summary>Gets the client's time, to the second.</summary>
    public required DateTimeOffset ClientTime { get; init; }

    /// <summary>Gets the key the client proposes for the session; <see langword="null" /> when it proposes none.</summary>
    public KerberosKey? Subkey { get; init; }

    /// <summary>Gets the client's initial sequence number; <see langword="null" /> when it gives none.</summary>
    public uint? SequenceNumber { get; init; }

    /// <summary>Gets the authorization data; empty when there is none.</summary>
    public IReadOnlyList<KerberosAuthorizationData> AuthorizationData { get; init; } = [];

    /// <summary>Encodes the authenticator, ready to encrypt. The bytes hold the subkey, when there is one; the caller zeroes them.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode(ApplicationTag, writer =>
    {
        KerberosAsn1.WriteVersion(writer, 0);
        KerberosAsn1.WriteField(writer, 1, ClientRealm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(writer, 2, ClientName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteOptionalField(writer, 3, Checksum, KerberosChecksum.Writer);
        KerberosAsn1.WriteField(writer, 4, ClientMicroseconds, KerberosAsn1.Int32Writer);
        KerberosAsn1.WriteField(writer, 5, ClientTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalField(writer, 6, Subkey, KerberosAsn1.KeyWriter);
        KerberosAsn1.WriteOptionalValueField(writer, 7, SequenceNumber, KerberosAsn1.UInt32Writer);
        KerberosAsn1.WriteOptionalSequenceOfField(writer, 8, AuthorizationData, KerberosAsn1.AuthorizationDataWriter);
    });

    /// <summary>Decodes an authenticator decrypted from an AP-REQ.</summary>
    /// <param name="bytes">The plaintext.</param>
    /// <returns>The authenticator; the caller disposes it.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" />, <see cref="KerberosMessageError.UnexpectedMessage" />
    /// or <see cref="KerberosMessageError.UnsupportedVersion" />.
    /// </exception>
    public static KerberosAuthenticator Decode(ReadOnlyMemory<byte> bytes) => KerberosAsn1.Decode(bytes, ApplicationTag, ContentsReader);

    /// <summary>Zeroes the subkey, when there is one.</summary>
    public void Dispose() => Subkey?.Dispose();

    private static KerberosAuthenticator ReadContents(AsnReader reader)
    {
        KerberosAsn1.ReadVersion(reader, 0);
        string clientRealm = KerberosAsn1.ReadField(reader, 1, KerberosAsn1.StringReader);
        KerberosPrincipalName clientName = KerberosAsn1.ReadField(reader, 2, KerberosPrincipalName.Reader);
        KerberosChecksum? checksum = KerberosAsn1.ReadOptionalField(reader, 3, KerberosChecksum.Reader);
        int clientMicroseconds = KerberosAsn1.ReadField(reader, 4, KerberosAsn1.Int32Reader);
        DateTimeOffset clientTime = KerberosAsn1.ReadField(reader, 5, KerberosAsn1.TimeReader);
        KerberosKey? subkey = KerberosAsn1.ReadOptionalField(reader, 6, KerberosAsn1.KeyReader);
        return KerberosAsn1.DisposeKeyOnFailure(subkey, () => new KerberosAuthenticator
        {
            ClientRealm = clientRealm,
            ClientName = clientName,
            Checksum = checksum,
            ClientMicroseconds = clientMicroseconds,
            ClientTime = clientTime,
            Subkey = subkey,
            SequenceNumber = KerberosAsn1.ReadOptionalValueField(reader, 7, KerberosAsn1.UInt32Reader),
            AuthorizationData = KerberosAsn1.ReadOptionalSequenceOfField(reader, 8, KerberosAsn1.AuthorizationDataReader),
        });
    }
}
