using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Stands in for a name server that never answers for <see cref="UpstreamCaseScreening.StalledLookupHost"/>
/// (test1592's <c>a-site-never-accessed.example.org</c>): its lookup waits on <paramref name="timeProvider"/>
/// until it is cancelled, as libcurl's lookup of that name stays unanswered while lib1592 removes its handle.
/// Every other name goes to <paramref name="resolver"/> (BL-2839, ADR-0722).
/// </summary>
/// <param name="resolver">Resolves every other name.</param>
/// <param name="timeProvider">The clock the stalled lookup waits on.</param>
internal sealed class StalledLookupDnsResolver(IDnsResolver resolver, TimeProvider timeProvider) : IDnsResolver
{
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (!string.Equals(host, UpstreamCaseScreening.StalledLookupHost, StringComparison.OrdinalIgnoreCase))
        {
            return await resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken).ConfigureAwait(false);
        return [];
    }
}
