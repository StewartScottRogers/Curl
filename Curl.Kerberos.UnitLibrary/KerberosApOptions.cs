namespace Curl.Kerberos;

/// <summary>
/// An AP-REQ's options (RFC 4120 section 5.5.1's <c>APOptions</c>), with RFC 4120's bit 0 as
/// the most significant bit.
/// </summary>
[Flags]
public enum KerberosApOptions : uint
{
    /// <summary>No option set.</summary>
    None = 0,

    /// <summary>Bit 1, <c>use-session-key</c>: the ticket is encrypted in the server's TGT session key (user-to-user).</summary>
    UseSessionKey = 0x40000000,

    /// <summary>Bit 2, <c>mutual-required</c>: the client wants an AP-REP.</summary>
    MutualRequired = 0x20000000,
}
