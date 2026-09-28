using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IDatagramConnector" /> for UDP: applies the <c>--connect-to</c>
/// mappings, resolves the host through the <c>--resolve</c> entries or the
/// <see cref="IDnsResolver" />, and opens a <see cref="UdpDatagramChannel" /> whose
/// <see cref="IDatagramChannel.ServerEndPoint" /> is the first resolved address that a socket
/// can be opened for, at the (mapped) port.
/// </summary>
public sealed class UdpDatagramConnector : IDatagramConnector
{
    private readonly IDnsResolver _dnsResolver;
    private readonly TimeProvider _timeProvider;
    private readonly Func<IPEndPoint, IDatagramChannel> _openChannel;
    private readonly ResolveOverrides _resolveOverrides;
    private readonly ConnectToMappings _connectToMappings;
    private readonly AddressFamily _addressFamily;

    /// <summary>
    /// Initializes a connector that opens <see cref="UdpDatagramChannel" /> sockets.
    /// </summary>
    /// <param name="dnsResolver">Resolves a host no <c>--resolve</c> entry answers for.</param>
    /// <param name="timeProvider">Measures the time curl reports in its exit 7 message.</param>
    /// <param name="resolveOverrides">
    /// The <c>--resolve</c> entries; <see langword="null" /> for <see cref="ResolveOverrides.None" />.
    /// </param>
    /// <param name="connectToMappings">
    /// The <c>--connect-to</c> mappings; <see langword="null" /> for <see cref="ConnectToMappings.None" />.
    /// </param>
    /// <param name="addressFamily">
    /// The family <c>-4</c> or <c>-6</c> chose, or <see cref="AddressFamily.Unspecified" /> for
    /// either: a host name is opened at that family's addresses only (BL-500).
    /// </param>
    public UdpDatagramConnector(
        IDnsResolver dnsResolver,
        TimeProvider timeProvider,
        ResolveOverrides? resolveOverrides = null,
        ConnectToMappings? connectToMappings = null,
        AddressFamily addressFamily = AddressFamily.Unspecified)
        : this(dnsResolver, timeProvider, serverEndPoint => new UdpDatagramChannel(serverEndPoint), resolveOverrides, connectToMappings, addressFamily)
    {
    }

    /// <summary>
    /// Initializes a connector that opens channels through <paramref name="openChannel" />,
    /// so a test drives resolution and endpoint selection without a socket.
    /// </summary>
    /// <param name="dnsResolver">Resolves a host no <c>--resolve</c> entry answers for.</param>
    /// <param name="timeProvider">Measures the time curl reports in its exit 7 message.</param>
    /// <param name="openChannel">Opens a channel to one server endpoint, or throws <see cref="SocketException" />.</param>
    /// <param name="resolveOverrides">
    /// The <c>--resolve</c> entries; <see langword="null" /> for <see cref="ResolveOverrides.None" />.
    /// </param>
    /// <param name="connectToMappings">
    /// The <c>--connect-to</c> mappings; <see langword="null" /> for <see cref="ConnectToMappings.None" />.
    /// </param>
    /// <param name="addressFamily">
    /// The family <c>-4</c> or <c>-6</c> chose, or <see cref="AddressFamily.Unspecified" /> for either.
    /// </param>
    internal UdpDatagramConnector(
        IDnsResolver dnsResolver,
        TimeProvider timeProvider,
        Func<IPEndPoint, IDatagramChannel> openChannel,
        ResolveOverrides? resolveOverrides = null,
        ConnectToMappings? connectToMappings = null,
        AddressFamily addressFamily = AddressFamily.Unspecified)
    {
        _dnsResolver = dnsResolver;
        _timeProvider = timeProvider;
        _openChannel = openChannel;
        _resolveOverrides = resolveOverrides ?? ResolveOverrides.None;
        _connectToMappings = connectToMappings ?? ConnectToMappings.None;
        _addressFamily = addressFamily;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Before anything is resolved, a <c>--resolve</c> entry that did not parse, or a
    /// <c>--connect-to</c> mapping that matches and whose destination does not parse, fails
    /// with exit 49 and curl 8.21.0's message, as <see cref="TcpConnector" /> fails it. A
    /// matching mapping's host and port are resolved and opened, and a <c>--resolve</c> entry
    /// for that host and port answers in place of the <see cref="IDnsResolver" /> (measured on
    /// curl 8.21.0 with <c>tftp://</c>).
    /// </para>
    /// <para>
    /// The other messages are curl 8.21.0's, cut to 255 characters as curl cuts them (ADR-0072):
    /// <c>Could not resolve host: &lt;host&gt;</c> for exit 6, naming the mapped host, and
    /// <c>Failed to connect to &lt;host&gt;:&lt;port&gt; after &lt;n&gt; ms: Could not connect
    /// to server</c> for exit 7 when no resolved address can have a socket opened for it, with
    /// <c> via &lt;mapped host&gt;:&lt;mapped port&gt;</c> before <c>after</c> when a mapping
    /// matched, where <c>n</c> is measured by the injected <see cref="TimeProvider" />.
    /// </para>
    /// </remarks>
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);

        var destination = _connectToMappings.Map(host, port);
        if ((_resolveOverrides.ParseError ?? destination.ParseError) is { } parseError)
        {
            return DatagramOpenResult.Failed(CurlExitCode.SetoptOptionSyntax, parseError);
        }

        var addresses = await ResolveAsync(destination, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            return DatagramOpenResult.Failed(
                CurlExitCode.CouldntResolveHost,
                CurlErrorBuffer.Truncate($"Could not resolve host: {destination.Host}"));
        }

        var openStarted = _timeProvider.GetTimestamp();
        var channel = OpenFirstAvailable(addresses, destination.Port);
        return channel is null
            ? OpenFailure(host, port, destination, openStarted)
            : DatagramOpenResult.Opened(channel);
    }

    private async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(ConnectDestination destination, CancellationToken cancellationToken) =>
        AddressFamilyFilter.Dialable(
            destination.Host,
            _resolveOverrides.Find(destination.Host, destination.Port)
                ?? await _dnsResolver.ResolveAsync(destination.Host, cancellationToken).ConfigureAwait(false),
            _addressFamily);

    private DatagramOpenResult OpenFailure(string host, int port, ConnectDestination destination, long openStarted)
    {
        var elapsedMilliseconds = (long)_timeProvider.GetElapsedTime(openStarted).TotalMilliseconds;
        var via = destination.IsMapped ? $" via {destination.Host}:{destination.Port}" : string.Empty;
        return DatagramOpenResult.Failed(
            CurlExitCode.CouldntConnect,
            $"Failed to connect to {host}:{port}{via} after {elapsedMilliseconds} ms: Could not connect to server");
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
