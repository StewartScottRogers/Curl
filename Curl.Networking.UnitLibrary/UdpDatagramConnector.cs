using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IDatagramConnector" /> for UDP: resolves the host and opens
/// a <see cref="UdpDatagramChannel" /> whose <see cref="IDatagramChannel.ServerEndPoint" />
/// is the first resolved address that a socket can be opened for, at the requested port.
/// </summary>
public sealed class UdpDatagramConnector : IDatagramConnector
{
    private readonly IDnsResolver _dnsResolver;
    private readonly TimeProvider _timeProvider;
    private readonly Func<IPEndPoint, IDatagramChannel> _openChannel;

    /// <summary>
    /// Initializes a connector that opens <see cref="UdpDatagramChannel" /> sockets.
    /// </summary>
    /// <param name="dnsResolver">Resolves the target host to addresses.</param>
    /// <param name="timeProvider">Measures the time curl reports in its exit 7 message.</param>
    public UdpDatagramConnector(IDnsResolver dnsResolver, TimeProvider timeProvider)
        : this(dnsResolver, timeProvider, serverEndPoint => new UdpDatagramChannel(serverEndPoint))
    {
    }

    /// <summary>
    /// Initializes a connector that opens channels through <paramref name="openChannel" />,
    /// so a test drives resolution and endpoint selection without a socket.
    /// </summary>
    /// <param name="dnsResolver">Resolves the target host to addresses.</param>
    /// <param name="timeProvider">Measures the time curl reports in its exit 7 message.</param>
    /// <param name="openChannel">Opens a channel to one server endpoint, or throws <see cref="SocketException" />.</param>
    internal UdpDatagramConnector(
        IDnsResolver dnsResolver,
        TimeProvider timeProvider,
        Func<IPEndPoint, IDatagramChannel> openChannel)
    {
        _dnsResolver = dnsResolver;
        _timeProvider = timeProvider;
        _openChannel = openChannel;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The messages are curl 8.21.0's, cut to 255 characters as curl cuts them (ADR-0071):
    /// <c>Could not resolve host: &lt;host&gt;</c> for
    /// exit 6, and <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms:
    /// Could not connect to server</c> for exit 7 when no resolved address can have a
    /// socket opened for it, where <c>n</c> is measured by the injected
    /// <see cref="TimeProvider" />.
    /// </remarks>
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);

        var addresses = await _dnsResolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return DatagramOpenResult.Failed(
                CurlExitCode.CouldntResolveHost,
                CurlErrorBuffer.Truncate($"Could not resolve host: {host}"));
        }

        var openStarted = _timeProvider.GetTimestamp();
        var channel = OpenFirstAvailable(addresses, port);
        if (channel is null)
        {
            var elapsedMilliseconds = (long)_timeProvider.GetElapsedTime(openStarted).TotalMilliseconds;
            return DatagramOpenResult.Failed(
                CurlExitCode.CouldntConnect,
                $"Failed to connect to {host}:{port} after {elapsedMilliseconds} ms: Could not connect to server");
        }

        return DatagramOpenResult.Opened(channel);
    }

    private IDatagramChannel? OpenFirstAvailable(IReadOnlyList<IPAddress> addresses, int port)
    {
        foreach (var address in addresses)
        {
            try
            {
                return _openChannel(new IPEndPoint(address, port));
            }
            catch (SocketException)
            {
                // An address family the host cannot open a socket for; try the next address.
            }
        }

        return null;
    }
}
