namespace Curl.Kerberos;

/// <summary>
/// A ticket's flags (RFC 4120 section 5.3's <c>TicketFlags</c>), with RFC 4120's bit 0 as
/// the most significant bit, as MIT's credential cache stores them.
/// </summary>
[Flags]
public enum KerberosTicketFlags : uint
{
    /// <summary>No flag set.</summary>
    None = 0,

    /// <summary>Bit 1, <c>forwardable</c>.</summary>
    Forwardable = 0x40000000,

    /// <summary>Bit 2, <c>forwarded</c>.</summary>
    Forwarded = 0x20000000,

    /// <summary>Bit 3, <c>proxiable</c>.</summary>
    Proxiable = 0x10000000,

    /// <summary>Bit 4, <c>proxy</c>.</summary>
    Proxy = 0x08000000,

    /// <summary>Bit 5, <c>may-postdate</c>.</summary>
    MayPostdate = 0x04000000,

    /// <summary>Bit 6, <c>postdated</c>.</summary>
    Postdated = 0x02000000,

    /// <summary>Bit 7, <c>invalid</c>.</summary>
    Invalid = 0x01000000,

    /// <summary>Bit 8, <c>renewable</c>.</summary>
    Renewable = 0x00800000,

    /// <summary>Bit 9, <c>initial</c>.</summary>
    Initial = 0x00400000,

    /// <summary>Bit 10, <c>pre-authent</c>.</summary>
    PreAuthenticated = 0x00200000,

    /// <summary>Bit 11, <c>hw-authent</c>.</summary>
    HardwareAuthenticated = 0x00100000,

    /// <summary>Bit 12, <c>transited-policy-checked</c>.</summary>
    TransitedPolicyChecked = 0x00080000,

    /// <summary>Bit 13, <c>ok-as-delegate</c>.</summary>
    OkAsDelegate = 0x00040000,

    /// <summary>Bit 15, <c>enc-pa-rep</c> (RFC 6806).</summary>
    EncryptedPreAuthenticationReply = 0x00010000,

    /// <summary>Bit 16, <c>anonymous</c> (RFC 8062).</summary>
    Anonymous = 0x00008000,
}
