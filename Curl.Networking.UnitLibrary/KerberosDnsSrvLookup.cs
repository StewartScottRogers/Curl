using Curl.Kerberos;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IKerberosSrvLookup" /> (ADR-0142, ADR-0176): SRV records through
/// the hand-built DNS client, <see cref="DnsServerResolver.ResolveServiceAsync" />. A failed
/// lookup, whatever its reason, is no records, as MIT's locator treats it.
/// </summary>
/// <param name="resolveService">Looks up a name's SRV records: <see cref="DnsServerResolver.ResolveServiceAsync" /> in production.</param>
public sealed class KerberosDnsSrvLookup(Func<string, CancellationToken, ValueTask<DnsServiceLookup>> resolveService) : IKerberosSrvLookup
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<KerberosSrvRecord>> LookUpAsync(string name, CancellationToken cancellationToken)
    {
        DnsServiceLookup lookup = await resolveService(name, cancellationToken).ConfigureAwait(false);
        return [.. lookup.Records.Select(record => new KerberosSrvRecord(record.Priority, record.Weight, record.Port, record.Target))];
    }
}
