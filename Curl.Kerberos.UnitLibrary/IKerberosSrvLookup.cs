namespace Curl.Kerberos;

/// <summary>
/// Looks up DNS SRV records, so KDC location needs no network in tests. Its implementation
/// over the hand-built DNS client is composed where ADR-0142's routing is wired.
/// </summary>
public interface IKerberosSrvLookup
{
    /// <summary>Looks up the SRV records of <paramref name="name" />.</summary>
    /// <param name="name">An absolute name without its trailing dot, e.g. <c>_kerberos._udp.EXAMPLE.COM</c>; no search list applies.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The records in answer order; empty when the name has none.</returns>
    Task<IReadOnlyList<KerberosSrvRecord>> LookUpAsync(string name, CancellationToken cancellationToken);
}
