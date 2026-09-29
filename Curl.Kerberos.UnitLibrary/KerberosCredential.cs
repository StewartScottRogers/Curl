namespace Curl.Kerberos;

/// <summary>
/// A ticket the client can use and the session key that goes with it: a ticket-granting
/// ticket from an AS exchange, or a service ticket from a TGS exchange or the credential
/// cache. <see cref="Dispose" /> zeroes the session key.
/// </summary>
public sealed class KerberosCredential : IDisposable
{
    /// <summary>Gets the principal the ticket was issued to.</summary>
    public required KerberosPrincipal Client { get; init; }

    /// <summary>Gets the principal the ticket is for, e.g. <c>HTTP/server.example.test@EXAMPLE.TEST</c>.</summary>
    public required KerberosPrincipal Server { get; init; }

    /// <summary>Gets the ticket, to send in an AP-REQ.</summary>
    public required KerberosTicket Ticket { get; init; }

    /// <summary>Gets the session key shared with <see cref="Server" />; the credential owns it.</summary>
    public required KerberosKey SessionKey { get; init; }

    /// <summary>Gets the ticket's flags.</summary>
    public required KerberosTicketFlags Flags { get; init; }

    /// <summary>Gets when the client first authenticated.</summary>
    public required DateTimeOffset AuthenticationTime { get; init; }

    /// <summary>Gets when the ticket expires.</summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Zeroes the session key.</summary>
    public void Dispose() => SessionKey.Dispose();

    /// <summary>Copies a credential out of a credential cache, session key included, so disposing either leaves the other intact.</summary>
    /// <param name="cached">The cached credential.</param>
    /// <returns>The credential; the caller disposes it.</returns>
    /// <exception cref="KerberosMessageException">The cached ticket does not decode.</exception>
    internal static KerberosCredential FromCache(CachedCredential cached) => new()
    {
        Client = cached.Client,
        Server = cached.Server,
        Ticket = KerberosTicket.Decode(cached.Ticket),
        SessionKey = new KerberosKey(cached.SessionKey.EncryptionType, cached.SessionKey.Value.ToArray()),
        Flags = cached.Flags,
        AuthenticationTime = cached.AuthenticationTime,
        EndTime = cached.EndTime,
    };
}
