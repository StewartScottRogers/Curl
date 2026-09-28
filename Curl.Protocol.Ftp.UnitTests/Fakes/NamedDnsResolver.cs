using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="IDnsResolver" /> that knows the names in <paramref name="names" /> and
/// nothing else, and records every name it was asked for.
/// </summary>
/// <param name="names">Each name and the addresses it resolves to, in order.</param>
public sealed class NamedDnsResolver(IReadOnlyDictionary<string, IPAddress[]> names) : IDnsResolver
{
    /// <summary>Gets every name asked for, in order.</summary>
    public List<string> Hosts { get; } = [];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        Hosts.Add(host);
        return ValueTask.FromResult<IReadOnlyList<IPAddress>>(names.TryGetValue(host, out IPAddress[]? addresses) ? addresses : []);
    }
}
