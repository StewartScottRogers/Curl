using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IDnsResolver" />: resolves through
/// <see cref="Dns.GetHostAddressesAsync(string, CancellationToken)" />, the operating
/// system's resolver.
/// </summary>
/// <remarks>
/// A host the system resolver cannot resolve yields an empty list rather than an
/// exception, which <see cref="TcpConnector" /> reports as
/// <see cref="CurlExitCode.CouldntResolveHost" />.
/// </remarks>
public sealed class SystemDnsResolver : IDnsResolver
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _lookUpHostAddresses;

    /// <summary>
    /// Initializes a resolver backed by <see cref="Dns" />.
    /// </summary>
    public SystemDnsResolver()
        : this(Dns.GetHostAddressesAsync)
    {
    }

    /// <summary>
    /// Initializes a resolver backed by <paramref name="lookUpHostAddresses" />, so a test
    /// can make the lookup fail without a network.
    /// </summary>
    /// <param name="lookUpHostAddresses">Looks up a host's addresses.</param>
    internal SystemDnsResolver(Func<string, CancellationToken, Task<IPAddress[]>> lookUpHostAddresses)
    {
        _lookUpHostAddresses = lookUpHostAddresses;
    }

    /// <summary>
    /// Resolves <paramref name="host" /> through the system resolver.
    /// </summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>
    /// The addresses in the order the system resolver returned them, or an empty list when
    /// it reports the host cannot be resolved.
    /// </returns>
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        try
        {
            return await _lookUpHostAddresses(host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
