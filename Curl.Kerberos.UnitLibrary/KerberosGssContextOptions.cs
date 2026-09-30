namespace Curl.Kerberos;

/// <summary>What an initiator's <see cref="KerberosGssContext" /> asks for.</summary>
public sealed class KerberosGssContextOptions
{
    /// <summary>
    /// Gets the flags the caller requests; <see cref="KerberosGssFlags.Delegation" /> is
    /// ignored here and decided by <see cref="Delegation" />. Curl asks for mutual
    /// authentication and replay detection, the default.
    /// </summary>
    public KerberosGssFlags RequestedFlags { get; init; } = KerberosGssFlags.MutualAuthentication | KerberosGssFlags.ReplayDetection;

    /// <summary>Gets when to forward the ticket-granting ticket.</summary>
    public KerberosDelegation Delegation { get; init; } = KerberosDelegation.None;

    /// <summary>
    /// Gets the forwarded ticket-granting ticket (a TGS answer with the <c>forwarded</c>
    /// flag) to send when <see cref="Delegation" /> allows it; <see langword="null" /> when
    /// there is none, which sends no delegation, as MIT does when its ticket-granting ticket
    /// is not forwardable. It stays the caller's.
    /// </summary>
    public KerberosCredential? ForwardedTicketGrantingTicket { get; init; }

    /// <summary>
    /// Gets the channel bindings' application data, as curl with MIT passes it over HTTPS:
    /// <c>tls-server-end-point:</c> followed by the server certificate's hash (RFC 5929
    /// section 4.1), with no initiator or acceptor address. The initial token's checksum
    /// then carries the MD5 of RFC 2744's <c>gss_channel_bindings_struct</c> holding it, as
    /// MIT computes it (ADR-0171); <see langword="null" /> sends zeros, as with no bindings.
    /// It stays the caller's.
    /// </summary>
    public byte[]? ChannelBindings { get; init; }
}
