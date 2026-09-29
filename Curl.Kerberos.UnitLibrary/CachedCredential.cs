namespace Curl.Kerberos;

/// <summary>
/// One credential in a credential cache: a ticket for <see cref="Server" />, the session key
/// that goes with it, and what the KDC said about it. MIT also stores its own configuration
/// in the cache as credentials whose server realm is <c>X-CACHECONF:</c>;
/// <see cref="IsConfigurationEntry" /> tells them apart.
/// </summary>
public sealed class CachedCredential
{
    /// <summary>The server realm MIT gives the configuration entries it stores as credentials.</summary>
    public const string ConfigurationRealm = "X-CACHECONF:";

    /// <summary>Gets the principal the ticket was issued to.</summary>
    public required KerberosPrincipal Client { get; init; }

    /// <summary>Gets the principal the ticket is for, e.g. <c>krbtgt/EXAMPLE.TEST@EXAMPLE.TEST</c> for the TGT.</summary>
    public required KerberosPrincipal Server { get; init; }

    /// <summary>Gets the session key shared with <see cref="Server" />.</summary>
    public required KerberosKey SessionKey { get; init; }

    /// <summary>Gets when the client first authenticated.</summary>
    public required DateTimeOffset AuthenticationTime { get; init; }

    /// <summary>Gets when the ticket becomes valid; the Unix epoch when the KDC gave none.</summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>Gets when the ticket expires.</summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Gets the last time the ticket can be renewed to; the Unix epoch when it is not renewable.</summary>
    public required DateTimeOffset RenewUntil { get; init; }

    /// <summary>Gets whether the ticket is encrypted in a session key rather than the server's key (user-to-user).</summary>
    public required bool IsEncryptedInSessionKey { get; init; }

    /// <summary>Gets the ticket's flags.</summary>
    public required KerberosTicketFlags Flags { get; init; }

    /// <summary>Gets the addresses the ticket is bound to; empty for an addressless ticket.</summary>
    public required IReadOnlyList<KerberosAddress> Addresses { get; init; }

    /// <summary>Gets the ticket's authorization data.</summary>
    public required IReadOnlyList<KerberosAuthorizationData> AuthorizationData { get; init; }

    /// <summary>Gets the DER-encoded <c>Ticket</c>; for a configuration entry, the configuration value.</summary>
    public required byte[] Ticket { get; init; }

    /// <summary>Gets the second ticket of a user-to-user exchange; empty when there is none.</summary>
    public required byte[] SecondTicket { get; init; }

    /// <summary>Gets whether this is one of MIT's configuration entries rather than a ticket.</summary>
    public bool IsConfigurationEntry => Server.Realm == ConfigurationRealm;
}
