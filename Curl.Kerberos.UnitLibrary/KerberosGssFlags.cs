namespace Curl.Kerberos;

/// <summary>
/// The context flags the initiator puts in RFC 4121 section 4.1.1.1's authenticator
/// checksum, with their GSS-API (RFC 2744) values, which are also the bits of that field.
/// </summary>
[Flags]
public enum KerberosGssFlags
{
    /// <summary>No flag set.</summary>
    None = 0,

    /// <summary><c>GSS_C_DELEG_FLAG</c>: the checksum carries a forwarded ticket-granting ticket.</summary>
    Delegation = 1,

    /// <summary><c>GSS_C_MUTUAL_FLAG</c>: the acceptor proves itself with an AP-REP.</summary>
    MutualAuthentication = 2,

    /// <summary><c>GSS_C_REPLAY_FLAG</c>: per-message tokens are checked against replay.</summary>
    ReplayDetection = 4,

    /// <summary><c>GSS_C_SEQUENCE_FLAG</c>: per-message tokens are checked for order.</summary>
    SequenceDetection = 8,

    /// <summary><c>GSS_C_CONF_FLAG</c>: Wrap tokens may be encrypted.</summary>
    Confidentiality = 16,

    /// <summary><c>GSS_C_INTEG_FLAG</c>: per-message tokens carry a keyed checksum.</summary>
    Integrity = 32,

    /// <summary>
    /// <c>GSS_C_TRANS_FLAG</c>: the context could be exported. Not an RFC 4121 flag, but MIT
    /// always sets it and sends it in the checksum, so it is always set here too (ADR-0171).
    /// </summary>
    Transfer = 256,
}
