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
/// A host the system resolver cannot resolve, or one longer than the 255 characters
/// <see cref="Dns" /> accepts, yields an empty list rather than an exception, which <see cref="TcpConnector" /> reports as
/// <see cref="CurlExitCode.CouldntResolveHost" />. An address literal <see cref="Dns" />
/// refuses, the any-address such as host <c>0</c>, resolves to itself.
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
    /// it reports the host cannot be resolved or refuses it as longer than 255 characters.
    /// An address literal the system resolver refuses - the any-address, such as host
    /// <c>0</c> - is returned as itself, so the connect fails as curl's does with
    /// <see cref="CurlExitCode.CouldntConnect" />.
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
        catch (ArgumentOutOfRangeException)
        {
            // Dns refuses a name over 255 characters; curl 8.21.0 accepts hosts up to 65535
            // bytes and reports such a host as exit 6 (BL-377).
            return [];
        }
        catch (ArgumentException) when (IPAddress.TryParse(host, out var literal))
        {
            // Dns refuses the any-address (0.0.0.0, written "0" in test1293); curl 8.21.0
            // resolves it to itself and fails the connect: curl http://0 -> exit 7,
            // "Failed to connect to 0.0.0.0:80" (measured 2026-10-11, BL-2084).
            return [literal];
        }
    }
}
