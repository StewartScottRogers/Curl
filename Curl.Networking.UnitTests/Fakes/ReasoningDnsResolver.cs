using System.Net;

namespace Curl.Networking.Fakes;

/// <summary>An <see cref="IDnsResolverWithFailureReason" /> that returns one fixed resolution.</summary>
/// <param name="resolution">What every lookup returns.</param>
public sealed class ReasoningDnsResolver(DnsResolution resolution) : IDnsResolverWithFailureReason
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult(resolution.Addresses);

    /// <inheritdoc />
    public ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult(resolution);
}
