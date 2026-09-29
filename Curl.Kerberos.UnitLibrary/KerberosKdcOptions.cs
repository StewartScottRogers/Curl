namespace Curl.Kerberos;

/// <summary>
/// What a client asks the KDC for (RFC 4120 section 5.4.1's <c>KDCOptions</c>), with RFC 4120's
/// bit 0 as the most significant bit, as <see cref="KerberosTicketFlags" /> numbers them.
/// </summary>
[Flags]
public enum KerberosKdcOptions : uint
{
    /// <summary>No option set.</summary>
    None = 0,

    /// <summary>Bit 1, <c>forwardable</c>.</summary>
    Forwardable = 0x40000000,

    /// <summary>Bit 2, <c>forwarded</c>.</summary>
    Forwarded = 0x20000000,

    /// <summary>Bit 3, <c>proxiable</c>.</summary>
    Proxiable = 0x10000000,

    /// <summary>Bit 4, <c>proxy</c>.</summary>
    Proxy = 0x08000000,

    /// <summary>Bit 5, <c>allow-postdate</c>.</summary>
    AllowPostdate = 0x04000000,

    /// <summary>Bit 6, <c>postdated</c>.</summary>
    Postdated = 0x02000000,

    /// <summary>Bit 8, <c>renewable</c>.</summary>
    Renewable = 0x00800000,

    /// <summary>Bit 15, <c>canonicalize</c> (RFC 6806).</summary>
    Canonicalize = 0x00010000,

    /// <summary>Bit 16, <c>request-anonymous</c> (RFC 8062).</summary>
    RequestAnonymous = 0x00008000,

    /// <summary>Bit 26, <c>disable-transited-check</c>.</summary>
    DisableTransitedCheck = 0x00000020,

    /// <summary>Bit 27, <c>renewable-ok</c>.</summary>
    RenewableOk = 0x00000010,

    /// <summary>Bit 28, <c>enc-tkt-in-skey</c>.</summary>
    EncryptTicketInSessionKey = 0x00000008,

    /// <summary>Bit 30, <c>renew</c>.</summary>
    Renew = 0x00000002,

    /// <summary>Bit 31, <c>validate</c>.</summary>
    Validate = 0x00000001,
}
