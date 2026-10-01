using Curl.Kerberos;

namespace Curl.Authentication;

/// <summary>
/// Gets the hand-built route's service ticket as MIT's GSS-API library does for a
/// host-based service: the principal <c>service/host@REALM</c>, with the host lower-cased
/// and the realm from <c>krb5.conf</c>'s <c>[domain_realm]</c> or <c>default_realm</c>, else
/// the credential cache's own; the ticket from the default credential cache, or by a TGS
/// exchange with its ticket-granting ticket, stored back in that cache so the next run asks
/// no KDC (ADR-0142, ADR-0176, ADR-0208).
/// </summary>
/// <param name="readConfiguration">Reads <c>krb5.conf</c>; called on each request, so nothing is read until Negotiate is answered.</param>
/// <param name="createCredentialCacheStore">Makes the store that finds, reads and writes the default credential cache.</param>
/// <param name="createKdcClient">Makes the KDC client for the configuration read.</param>
public sealed class KerberosServiceTicketSource(
    Func<KerberosConfiguration> readConfiguration,
    Func<CredentialCacheStore> createCredentialCacheStore,
    Func<KerberosConfiguration, KerberosKdcClient> createKdcClient)
{
    /// <summary>MIT's <c>KRB5_NT_SRV_HST</c>, the name type of a host-based service's principal.</summary>
    public const int HostBasedServiceNameType = 3;

    /// <summary>Gets a ticket for <paramref name="serviceName" /> on <paramref name="hostName" />.</summary>
    /// <param name="serviceName">The service, e.g. <c>HTTP</c>.</param>
    /// <param name="hostName">The host.</param>
    /// <param name="cancellationToken">Cancels the KDC exchange.</param>
    /// <returns>The ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosConfigurationException"><c>krb5.conf</c> cannot be read.</exception>
    /// <exception cref="KerberosFileException">The credential cache cannot be read.</exception>
    /// <exception cref="KerberosKdcException">No ticket could be got.</exception>
    public async Task<KerberosCredential> GetAsync(string serviceName, string hostName, CancellationToken cancellationToken)
    {
        KerberosConfiguration configuration = readConfiguration();
        CredentialCacheStore store = createCredentialCacheStore();
        string cacheName = store.DefaultCacheName();
        string host = hostName.ToLowerInvariant();
        string realm = configuration.RealmOfHost(host) ?? DefaultPrincipalRealm(store, cacheName);
        KerberosPrincipal server = new(HostBasedServiceNameType, realm, [serviceName, host]);
        return await createKdcClient(configuration).GetServiceTicketAsync(server, store, cacheName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a forwarded ticket-granting ticket for <c>--delegation</c> from the default
    /// credential cache's ticket-granting ticket, as MIT's <c>krb5_fwd_tgt_creds</c> does
    /// (ADR-0210).
    /// </summary>
    /// <param name="cancellationToken">Cancels the KDC exchange.</param>
    /// <returns>The forwarded ticket-granting ticket; the caller disposes it.</returns>
    /// <exception cref="KerberosConfigurationException"><c>krb5.conf</c> cannot be read.</exception>
    /// <exception cref="KerberosFileException">The credential cache cannot be read.</exception>
    /// <exception cref="KerberosKdcException">No forwarded ticket could be got, e.g. the ticket-granting ticket is not forwardable.</exception>
    public async Task<KerberosCredential> GetForwardedTicketGrantingTicketAsync(CancellationToken cancellationToken)
    {
        KerberosConfiguration configuration = readConfiguration();
        using CredentialCache cache = createCredentialCacheStore().ReadDefault();
        return await createKdcClient(configuration).GetForwardedTicketGrantingTicketAsync(cache, cancellationToken).ConfigureAwait(false);
    }

    private static string DefaultPrincipalRealm(CredentialCacheStore store, string cacheName)
    {
        using CredentialCache cache = store.Read(cacheName);
        return cache.DefaultPrincipal.Realm;
    }
}
