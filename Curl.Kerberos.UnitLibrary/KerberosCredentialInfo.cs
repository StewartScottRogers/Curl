using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// One forwarded credential in a KRB-CRED (RFC 4120's <c>KrbCredInfo</c>): the ticket's
/// session key and who and what the ticket is for. The ASN.1 makes every field but the key
/// optional; this library writes and reads the client, server, flags, authentication time
/// and end time always, the start and renew-until times when given, and no addresses.
/// <see cref="Dispose" /> zeroes the key.
/// </summary>
public sealed class KerberosCredentialInfo : IDisposable
{
    /// <summary>Reads a <c>KrbCredInfo</c>.</summary>
    internal static readonly Func<AsnReader, KerberosCredentialInfo> Reader = new Func<AsnReader, KerberosCredentialInfo>(Read);

    /// <summary>Writes a <c>KrbCredInfo</c>.</summary>
    internal static readonly Action<AsnWriter, KerberosCredentialInfo> Writer = new Action<AsnWriter, KerberosCredentialInfo>(Write);

    /// <summary>Gets the ticket's session key; the info owns it.</summary>
    public required KerberosKey Key { get; init; }

    /// <summary>Gets the client's realm.</summary>
    public required string ClientRealm { get; init; }

    /// <summary>Gets the client's name.</summary>
    public required KerberosPrincipalName ClientName { get; init; }

    /// <summary>Gets the ticket's flags.</summary>
    public required KerberosTicketFlags Flags { get; init; }

    /// <summary>Gets when the client first authenticated.</summary>
    public required DateTimeOffset AuthenticationTime { get; init; }

    /// <summary>Gets when the ticket becomes valid; <see langword="null" /> when not given.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Gets when the ticket expires.</summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Gets until when the ticket may be renewed; <see langword="null" /> when not given.</summary>
    public DateTimeOffset? RenewUntil { get; init; }

    /// <summary>Gets the server's realm.</summary>
    public required string ServerRealm { get; init; }

    /// <summary>Gets the server's name, e.g. <c>krbtgt/EXAMPLE.TEST</c>.</summary>
    public required KerberosPrincipalName ServerName { get; init; }

    /// <summary>Zeroes the key.</summary>
    public void Dispose() => Key.Dispose();

    private static KerberosCredentialInfo Read(AsnReader reader) => KerberosAsn1.ReadSequence(reader, inner =>
    {
        KerberosKey key = KerberosAsn1.ReadField(inner, 0, KerberosAsn1.KeyReader);
        return KerberosAsn1.DisposeKeyOnFailure(key, () => new KerberosCredentialInfo
        {
            Key = key,
            ClientRealm = KerberosAsn1.ReadField(inner, 1, KerberosAsn1.StringReader),
            ClientName = KerberosAsn1.ReadField(inner, 2, KerberosPrincipalName.Reader),
            Flags = (KerberosTicketFlags)KerberosAsn1.ReadField(inner, 3, KerberosAsn1.FlagsReader),
            AuthenticationTime = KerberosAsn1.ReadField(inner, 4, KerberosAsn1.TimeReader),
            StartTime = KerberosAsn1.ReadOptionalValueField(inner, 5, KerberosAsn1.TimeReader),
            EndTime = KerberosAsn1.ReadField(inner, 6, KerberosAsn1.TimeReader),
            RenewUntil = KerberosAsn1.ReadOptionalValueField(inner, 7, KerberosAsn1.TimeReader),
            ServerRealm = KerberosAsn1.ReadField(inner, 8, KerberosAsn1.StringReader),
            ServerName = KerberosAsn1.ReadField(inner, 9, KerberosPrincipalName.Reader),
        });
    });

    private static void Write(AsnWriter writer, KerberosCredentialInfo info) => KerberosAsn1.WriteSequence(writer, inner =>
    {
        KerberosAsn1.WriteField(inner, 0, info.Key, KerberosAsn1.KeyWriter);
        KerberosAsn1.WriteField(inner, 1, info.ClientRealm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(inner, 2, info.ClientName, KerberosPrincipalName.Writer);
        KerberosAsn1.WriteField(inner, 3, (uint)info.Flags, KerberosAsn1.FlagsWriter);
        KerberosAsn1.WriteField(inner, 4, info.AuthenticationTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(inner, 5, info.StartTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(inner, 6, info.EndTime, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteOptionalValueField(inner, 7, info.RenewUntil, KerberosAsn1.TimeWriter);
        KerberosAsn1.WriteField(inner, 8, info.ServerRealm, KerberosAsn1.StringWriter);
        KerberosAsn1.WriteField(inner, 9, info.ServerName, KerberosPrincipalName.Writer);
    });
}
