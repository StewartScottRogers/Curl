using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IDnsResolver" /> that returns a fixed list, or throws what it is given.
/// </summary>
/// <param name="addresses">The addresses every lookup returns.</param>
public sealed class FakeDnsResolver(params IPAddress[] addresses) : IDnsResolver
{
    /// <summary>Gets or sets an exception every lookup throws instead of returning.</summary>
    public Exception? ExceptionToThrow { get; init; }

    /// <summary>Gets the hosts every lookup of which returns no address, as a name that does not resolve.</summary>
    public IReadOnlySet<string> HostsWithNoAddress { get; init; } = new HashSet<string>();

    /// <summary>Gets the hosts looked up, in order.</summary>
    public List<string> ResolvedHosts { get; } = [];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ResolvedHosts.Add(host);
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return ValueTask.FromResult<IReadOnlyList<IPAddress>>(HostsWithNoAddress.Contains(host) ? [] : addresses);
    }
}
