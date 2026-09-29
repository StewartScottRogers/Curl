namespace Curl.Authentication;

/// <summary>
/// The object identifiers a SPNEGO token names: SPNEGO's own (RFC 4178) and the mechanisms
/// a NegTokenInit may offer, with the mechanism list the hand-built Negotiate route offers.
/// </summary>
internal static class SpnegoMechanism
{
    /// <summary>SPNEGO itself, <c>1.3.6.1.5.5.2</c>: the <c>thisMech</c> of the initial token.</summary>
    public const string Spnego = "1.3.6.1.5.5.2";

    /// <summary>Kerberos V5 (RFC 1964), <c>1.2.840.113554.1.2.2</c>.</summary>
    public const string KerberosV5 = "1.2.840.113554.1.2.2";

    /// <summary>The legacy Microsoft Kerberos V5 identifier, <c>1.2.840.48018.1.2.2</c>, which SSPI offers first.</summary>
    public const string MicrosoftKerberosV5 = "1.2.840.48018.1.2.2";

    /// <summary>NTLMSSP, <c>1.3.6.1.4.1.311.2.2.10</c>.</summary>
    public const string Ntlmssp = "1.3.6.1.4.1.311.2.2.10";

    /// <summary>
    /// Gets the mechanism list curl 8.18.0 with MIT krb5 1.22.1 (no <c>gss-ntlmssp</c>) sent
    /// in its NegTokenInit, measured on 2026-09-28: Kerberos V5 alone (ADR-0167).
    /// </summary>
    public static IReadOnlyList<string> MitKerberosMechanismTypes { get; } = [KerberosV5];
}
