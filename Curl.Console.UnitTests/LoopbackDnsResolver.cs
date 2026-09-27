using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>A resolver that answers every host with <see cref="IPAddress.Loopback" />.</summary>
internal sealed class LoopbackDnsResolver : IDnsResolver
{
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
}
