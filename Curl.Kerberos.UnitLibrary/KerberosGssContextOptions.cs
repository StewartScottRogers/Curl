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
}
