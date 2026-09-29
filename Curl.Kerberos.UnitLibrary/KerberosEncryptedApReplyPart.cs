using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// The plaintext of an AP-REP (RFC 4120's <c>EncAPRepPart</c>, <c>[APPLICATION 27]</c>): the
/// client's own time echoed back, with the server's optional subkey and sequence number.
/// <see cref="Dispose" /> zeroes the subkey.
/// </summary>
public sealed class KerberosEncryptedApReplyPart : IDisposable
{
    private const int ApplicationTag = 27;

    private static readonly Func<AsnReader, KerberosEncryptedApReplyPart> ContentsReader = new Func<AsnReader, KerberosEncryptedApReplyPart>(ReadContents);

    /// <summary>Gets the client's time from the authenticator, to the second.</summary>
    public required DateTimeOffset ClientTime { get; init; }

    /// <summary>Gets the microseconds past <see cref="ClientTime" />.</summary>
    public required int ClientMicroseconds { get; init; }

    /// <summary>Gets the key the server chose for the session; <see langword="null" /> when it chose none.</summary>
    public KerberosKey? Subkey { get; init; }

    /// <summary>Gets the server's initial sequence number; <see langword="null" /> when it gives none.</summary>
    public uint? SequenceNumber { get; init; }

    /// <summary>Encodes the part, ready to encrypt. The bytes hold the subkey, when there is one; the caller zeroes them.</summary>
    /// <returns>The DER encoding.</returns>
    public byte[] Encode() => KerberosAsn1.Encode(ApplicationTag, writer =>
    {
        KerberosAsn1.WriteField(writer, 0, ClientTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(writer, 1, ClientMicroseconds, KerberosAsn1.Int32Writer);
        KerberosAsn1.WriteOptionalField(writer, 2, Subkey, KerberosAsn1.KeyWriter);
        KerberosAsn1.WriteOptionalValueField(writer, 3, SequenceNumber, KerberosAsn1.UInt32Writer);
    });

    /// <summary>Decodes a part decrypted from an AP-REP.</summary>
    /// <param name="bytes">The plaintext.</param>
    /// <returns>The part; the caller disposes it.</returns>
    /// <exception cref="KerberosMessageException">
    /// <see cref="KerberosMessageError.Malformed" /> or <see cref="KerberosMessageError.UnexpectedMessage" />.
    /// </exception>
    public static KerberosEncryptedApReplyPart Decode(ReadOnlyMemory<byte> bytes) => KerberosAsn1.Decode(bytes, ApplicationTag, ContentsReader);

    /// <summary>Zeroes the subkey, when there is one.</summary>
    public void Dispose() => Subkey?.Dispose();

    private static KerberosEncryptedApReplyPart ReadContents(AsnReader reader)
    {
        DateTimeOffset clientTime = KerberosAsn1.ReadField(reader, 0, KerberosAsn1.TimeReader);
        int clientMicroseconds = KerberosAsn1.ReadField(reader, 1, KerberosAsn1.Int32Reader);
        KerberosKey? subkey = KerberosAsn1.ReadOptionalField(reader, 2, KerberosAsn1.KeyReader);
        return KerberosAsn1.DisposeKeyOnFailure(subkey, () => new KerberosEncryptedApReplyPart
        {
            ClientTime = clientTime,
            ClientMicroseconds = clientMicroseconds,
            Subkey = subkey,
            SequenceNumber = KerberosAsn1.ReadOptionalValueField(reader, 3, KerberosAsn1.UInt32Reader),
        });
    }
}
