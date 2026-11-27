using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    // Every TCP dial goes through this one: bound as localBinding asks, its -v bind lines on events, or the dialer as given.
    private ITcpDialer BindingDialer(ITransferEvents events) => LocalAddressChooser() is { } addressChooser
        ? new LocalBindingTcpDialer(tcpDialer, addressChooser, events)
        : tcpDialer;

    // The race's dialer: BindingDialer's, asking the target's socket opener before each attempt when it has one (ADR-0903).
    private ITcpDialer AttemptDialer(ConnectTarget target) => target.SocketOpener is { } opener
        ? new SocketOpenerTcpDialer(BindingDialer(target.Events), opener)
        : BindingDialer(target.Events);

    // Chooses the local address TCP dials and QUIC's UDP sockets bind; null when localBinding is.
    private LocalBindingAddressChooser? LocalAddressChooser() => localBinding is null
        ? null
        : new LocalBindingAddressChooser(localBinding, networkInterfaceLookup ?? new SystemNetworkInterfaceLookup(), dnsResolver);

    /// <summary>
    /// Races the address families of <paramref name="addresses" /> through an
    /// <see cref="AddressFamilyRace" /> and returns the first connection, or <see langword="null" />
    /// with the <see cref="SocketError" /> of the attempt that failed last, which curl keeps as
    /// <c>CURLINFO_OS_ERRNO</c>, and its <see cref="LocalBindFailure" /> when its local end could not be bound.
    /// </summary>
    private async ValueTask<(DialedSocket? Dialed, SocketError LastError, LocalBindFailure? LastBindFailure)> DialFirstReachableAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        string hostName,
        ConnectTarget target,
        CancellationToken cancellationToken,
        ConnectAttemptTraceEvents? trace = null)
    {
        // No server listens on port 0: curl 8.22.0 fails it at once with exit 7 "Could not connect
        // to server", so no socket is dialled for it (measured, BL-3172 Notes).
        if (port == 0)
        {
            return (null, SocketError.ConnectionRefused, null);
        }

        var race = new AddressFamilyRace(AttemptDialer(target), timeProvider, HappyEyeballsTimeout, target.Events, new NetworkDiagnosticLog(target.DiagnosticLog), trace);
        var (dialed, remoteEndPoint, lastError, lastBindFailure) = await race.DialAsync(addresses, port, cancellationToken).ConfigureAwait(false);
        return dialed is null
            ? (null, lastError, lastBindFailure)
            : (new DialedSocket(dialed.Connection, dialed.LocalEndPoint, hostName, remoteEndPoint) { TracesHappyEyeballsFilter = trace?.TracesHappyEyeballs == true, TracesTcpFilter = trace?.TracesTcp == true, EarlierSocketError = lastError }, SocketError.Success, null);
    }

    /// <summary>
    /// The reason that ends a failed connect's message: curl's text for <c>CURLE_INTERFACE_FAILED</c> or
    /// <c>CURLE_BAD_FUNCTION_ARGUMENT</c> when the last address failed to bind its local end with
    /// one of them, <c>Could not connect to server</c> otherwise (measured, BL-600 Notes).
    /// </summary>
    // curl 8.22.0 names an IPv6 zone-id host without its zone: [::1%25lo] reads ::1 (BL-3182).
    private static string HostWithoutZoneId(string host) =>
        host.IndexOf('%', StringComparison.Ordinal) is var zone and > 0 && host.Contains(':') ? host[..zone] : host;

    private static string DialFailureText(LocalBindFailure? lastBindFailure) => lastBindFailure switch
    {
        LocalBindFailure.InterfaceFailed => "Failed binding local connection end",
        LocalBindFailure.BadArgument => "A libcurl function was given a bad argument",
        _ => "Could not connect to server",
    };

    /// <summary>
    /// The failure for a dial that reached no address, its message also reported on
    /// <paramref name="events" /> as curl's <c>-v</c> repeats it: exit 45 or exit 43 when the last
    /// address failed to bind its local end with <see cref="LocalBindFailure.InterfaceFailed" /> or
    /// <see cref="LocalBindFailure.BadArgument" />, exit 28 when the system gave up on the last
    /// attempt (<see cref="SocketError.TimedOut" />), as curl 8.21.0 and 8.18.0 end it with the same
    /// message (measured, BL-797 Notes), else exit 7, marked refused when the last
    /// attempt was refused, as <c>--retry-connrefused</c> reads curl's <c>CURLINFO_OS_ERRNO</c>.
    /// It carries the start and lookup timestamps, as curl 8.21.0 still reports
    /// <c>%{time_namelookup}</c> after a refused connect (measured, ADR-0091).
    /// </summary>
    private static ConnectResult DialFailure(ITransferEvents events, SocketError lastError, LocalBindFailure? lastBindFailure, ConnectTimings timings, string errorMessage)
    {
        events.ReportInfo(errorMessage);
        return lastBindFailure switch
        {
            LocalBindFailure.InterfaceFailed => ConnectResult.Failed(CurlExitCode.InterfaceFailed, errorMessage, timings),
            LocalBindFailure.BadArgument => ConnectResult.Failed(CurlExitCode.BadFunctionArgument, errorMessage, timings),
            _ when lastError == SocketError.ConnectionRefused => ConnectResult.Refused(errorMessage, timings),
            _ when lastError == SocketError.TimedOut => ConnectResult.Failed(CurlExitCode.OperationTimedOut, errorMessage, timings, lastSocketError: lastError),
            _ => ConnectResult.Failed(CurlExitCode.CouldntConnect, errorMessage, timings, lastSocketError: lastError),
        };
    }

    /// <summary>
    /// A dialled TCP connection with the name and address it was dialled for: the connection
    /// the transfer talks over, which becomes the proxy's TLS stream through an HTTPS proxy.
    /// </summary>
    /// <remarks>
    /// Through a Unix domain socket the host name is the socket's path, the two end points are
    /// <see langword="null" />, <paramref name="UnixSocketRemoteIp" /> is what curl shows instead and
    /// <paramref name="UnixSocketPath" /> is the whole path, which the HTTP handler names in its
    /// left-intact line (BL-884).
    /// </remarks>
    private sealed record DialedSocket(
        IConnection Connection,
        IPEndPoint? LocalEndPoint,
        string HostName,
        IPEndPoint? RemoteEndPoint,
        string? UnixSocketRemoteIp = null,
        string? UnixSocketPath = null)
    {
        /// <summary>
        /// Gets the <c>--connect-to</c> or alt-svc destination the socket was dialled to in
        /// place of the target's host and port, which the HTTP handler names in its left-intact
        /// line (BL-975); <see langword="null" /> when the socket went to the target itself or a proxy.
        /// </summary>
        public ConnectDestination? MappedDestination { get; init; }

        /// <summary>
        /// Gets the socket error of an earlier address's failed attempt that this address then
        /// outdid, which curl keeps as <c>CURLINFO_OS_ERRNO</c> after the connect succeeds (BL-3328);
        /// <see cref="SocketError.Success" /> when no attempt failed first.
        /// </summary>
        public SocketError EarlierSocketError { get; init; }

        /// <summary>
        /// Gets a value indicating whether the socket was dialled with the <c>[HAPPY-EYEBALLS]</c>
        /// lines written, so its filter's removal is written once the connection is established (BL-1161).
        /// </summary>
        public bool TracesHappyEyeballsFilter { get; init; }

        /// <summary>
        /// Gets a value indicating whether the socket was dialled with the <c>[TCP]</c> lines written,
        /// so a plain HTTP connection over it writes its I/O's too (BL-1195).
        /// </summary>
        public bool TracesTcpFilter { get; init; }

        /// <summary>
        /// Gets the <c>[HTTP-PROXY]</c> and <c>[H1-PROXY]</c> lines of the CONNECT tunnel opened over
        /// the socket through a plain HTTP proxy (BL-1193); <see langword="null" /> for any other socket.
        /// </summary>
        public HttpProxyTunnelTrace? TunnelTrace { get; init; }
    }
}
