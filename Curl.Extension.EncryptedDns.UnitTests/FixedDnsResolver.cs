using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns;

/// <summary>A resolver that answers every name with one address and counts the look-ups.</summary>
internal sealed class FixedDnsResolver(IPAddress address) : IDnsResolver
{
    public int LookupCount { get; private set; }

    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        LookupCount++;
        return ValueTask.FromResult<IReadOnlyList<IPAddress>>([address]);
    }
}
