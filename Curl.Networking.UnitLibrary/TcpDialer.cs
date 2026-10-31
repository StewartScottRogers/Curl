using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITcpDialer" />: connects a TCP <see cref="Socket" /> with the
/// <see cref="TcpSocketOptions" /> it was created with and returns it as a
/// <see cref="StreamConnection" /> over a <see cref="NetworkStream" /> that owns the socket,
/// with the local end point the socket was bound to; and connects a Unix domain socket the same
/// way, without the TCP options (BL-507).
/// </summary>
/// <param name="socketOptions">The options set on every socket before it connects.</param>
public sealed class TcpDialer(TcpSocketOptions socketOptions) : ITcpDialer
{
    // Linux's <asm-generic/socket.h>: SOL_SOCKET and SO_BINDTODEVICE.
    private const int LinuxSolSocket = 1;
    private const int LinuxSoBindToDevice = 25;

    private Action<nint>? socketCreated;
    private Func<CancellationToken, ValueTask>? failedSocketRelease;

    /// <summary>
    /// Creates a dialer with curl's default socket options: <c>TCP_NODELAY</c> and
    /// <c>SO_KEEPALIVE</c> both on.
    /// </summary>
    public TcpDialer()
        : this(new TcpSocketOptions())
    {
    }

    /// <summary>Gets the options set on every socket before it connects.</summary>
    public TcpSocketOptions SocketOptions { get; } = socketOptions ?? throw new ArgumentNullException(nameof(socketOptions));

    /// <inheritdoc />
    /// <remarks>
    /// The dialer returned has this one's <see cref="SocketOptions" /> and hands
    /// <paramref name="socketCreated" /> each socket's handle once its options are set and before it connects
    /// (<see cref="PrepareToConnect" />), the Unix domain socket's included.
    /// </remarks>
    public ITcpDialer PublishingSocketsTo(Action<nint> socketCreated)
    {
        ArgumentNullException.ThrowIfNull(socketCreated);
        return new TcpDialer(SocketOptions) { socketCreated = socketCreated, failedSocketRelease = failedSocketRelease };
    }

    /// <inheritdoc />
    /// <remarks>
    /// The dialer returned has this one's <see cref="SocketOptions" /> and publishing hook, and awaits
    /// <paramref name="released" /> before closing a TCP socket whose dial failed (<see cref="CloseFailedSocketAsync" />).
    /// </remarks>
    public ITcpDialer HoldingFailedSocketsUntil(Func<CancellationToken, ValueTask> released)
    {
        ArgumentNullException.ThrowIfNull(released);
        return new TcpDialer(SocketOptions) { socketCreated = socketCreated, failedSocketRelease = released };
    }

    /// <summary>
    /// Closes a TCP socket whose dial failed, first awaiting the hook <see cref="HoldingFailedSocketsUntil" /> gave, if any.
    /// </summary>
    /// <param name="socket">The socket that failed to connect.</param>
    /// <param name="cancellationToken">The dial's cancellation token, handed to the hook.</param>
    /// <returns>A task that completes once the socket is closed.</returns>
    internal async ValueTask CloseFailedSocketAsync(Socket socket, CancellationToken cancellationToken)
    {
        if (failedSocketRelease is not null)
        {
            await failedSocketRelease(cancellationToken).ConfigureAwait(false);
        }

        socket.Dispose();
    }

    /// <summary>
    /// Sets <see cref="SocketOptions" /> on a TCP socket about to connect (<see cref="ApplySocketOptions" />), then
    /// hands its handle to the hook <see cref="PublishingSocketsTo" /> gave, if any.
    /// </summary>
    /// <param name="socket">A TCP socket not yet connected.</param>
    /// <param name="events">Where a refused <c>--vlan-priority</c> is warned of; none by default.</param>
    internal void PrepareToConnect(Socket socket, ITransferEvents? events = null)
    {
        ApplySocketOptions(socket, events);
        PublishCreated(socket);
    }

    /// <summary>Hands <paramref name="socket" />'s handle to the hook <see cref="PublishingSocketsTo" /> gave, if any.</summary>
    /// <param name="socket">A socket not yet connected.</param>
    internal void PublishCreated(Socket socket) => socketCreated?.Invoke(socket.Handle);

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083: every line after the argument check needs a
    /// connected TCP socket, so the loopback test in the Integration run measures it. The
    /// options it sets are <see cref="ApplySocketOptions" />'s, which unit tests measure.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        return DialBoundAsync(endPoint, null, 0, NoTransferEvents.Instance, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083, as <see cref="DialAsync" /> is. The bind is
    /// <see cref="BindLocalEnd" />'s, which unit tests measure.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        ArgumentNullException.ThrowIfNull(localEndPoint);

        return DialBoundAsync(endPoint, localEndPoint, localPortCount, NoTransferEvents.Instance, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083, as <see cref="DialAsync" /> is. The bind and its lines are
    /// <see cref="BindLocalEnd" />'s, which unit tests measure.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, ITransferEvents events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        ArgumentNullException.ThrowIfNull(localEndPoint);
        ArgumentNullException.ThrowIfNull(events);

        return DialBoundAsync(endPoint, localEndPoint, localPortCount, events, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083, as <see cref="DialAsync" /> is. The device bind is
    /// <see cref="TryBindToDevice" />'s, and the address bind <see cref="BindLocalEnd" />'s.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public ValueTask<DialedTcpConnection> DialFromDeviceAsync(
        IPEndPoint endPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        ArgumentNullException.ThrowIfNull(deviceName);
        ArgumentNullException.ThrowIfNull(chooseLocalEndAsync);
        ArgumentNullException.ThrowIfNull(events);

        return DialDeviceBoundAsync(endPoint, deviceName, bindsAddressAfterDevice, chooseLocalEndAsync, localPortCount, events, cancellationToken);
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private async ValueTask<DialedTcpConnection> DialDeviceBoundAsync(
        IPEndPoint endPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, SocketOptions.SocketProtocol);
        try
        {
            PrepareToConnect(socket, events);
            await BindDeviceOrLocalEndAsync(
                deviceName,
                bindsAddressAfterDevice,
                [ExcludeFromCodeCoverage(Justification = "ADR-0083: binds the real socket, as its method does.")] (string name) => TryBindToDevice(socket, name),
                chooseLocalEndAsync,
                [ExcludeFromCodeCoverage(Justification = "ADR-0083: binds the real socket, as its method does.")] (IPEndPoint localEndPoint) => BindLocalEnd(socket, localEndPoint, localPortCount, events),
                events,
                cancellationToken).ConfigureAwait(false);

            socket = await ConnectFromBoundEndAsync(socket, endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await CloseFailedSocketAsync(socket, cancellationToken).ConfigureAwait(false);
            throw;
        }

        return Connected(socket, endPoint);
    }

    /// <summary>
    /// Binds a socket to the device <paramref name="deviceName" /> through <paramref name="tryBindToDevice" />,
    /// as libcurl's <c>bindlocal</c> tries <c>SO_BINDTODEVICE</c>, writing curl's <c>-v</c> line
    /// <c>socket successfully bound to interface '&lt;name&gt;'</c> when that bind is the whole binding
    /// (BL-1076); otherwise binds the local end <paramref name="chooseLocalEndAsync" /> chooses through
    /// <paramref name="bindLocalEnd" />.
    /// </summary>
    /// <remarks>
    /// curl 8.18.0 on Linux writes the line for a plain or <c>if!</c> name whose device bind succeeds, before
    /// the connect, so it stands even when the connect is then refused; <c>ifhost!</c> binds the device
    /// silently and goes on to its host's address, and a refused device bind falls back to the interface's
    /// address (BL-1076 Notes).
    /// </remarks>
    /// <param name="deviceName">The interface to bind the socket to.</param>
    /// <param name="bindsAddressAfterDevice"><see langword="true" /> for <c>ifhost!</c>.</param>
    /// <param name="tryBindToDevice">Binds the socket to the named device, answering whether it did.</param>
    /// <param name="chooseLocalEndAsync">Chooses the local address and first port, only when one is to be bound.</param>
    /// <param name="bindLocalEnd">Binds the socket's local end to the end point chosen.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <param name="cancellationToken">Cancels the choice of local end.</param>
    /// <returns>A task that completes once the socket is bound.</returns>
    internal static async ValueTask BindDeviceOrLocalEndAsync(
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<string, bool> tryBindToDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        Action<IPEndPoint> bindLocalEnd,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        if (tryBindToDevice(deviceName) && !bindsAddressAfterDevice)
        {
            // libcurl says so only for a name bound as a device alone, never for ifhost!.
            events.ReportInfo(LocalBindLines.DeviceBound(deviceName));
            return;
        }

        var localEndPoint = await chooseLocalEndAsync(cancellationToken).ConfigureAwait(false);
        bindLocalEnd(localEndPoint);
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private async ValueTask<DialedTcpConnection> DialBoundAsync(IPEndPoint endPoint, IPEndPoint? bindTo, int localPortCount, ITransferEvents events, CancellationToken cancellationToken)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, SocketOptions.SocketProtocol);
        try
        {
            PrepareToConnect(socket, events);
            if (bindTo is not null)
            {
                BindLocalEnd(socket, bindTo, localPortCount, events);
            }

            socket = await ConnectFromBoundEndAsync(socket, endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await CloseFailedSocketAsync(socket, cancellationToken).ConfigureAwait(false);
            throw;
        }

        return Connected(socket, endPoint);
    }

    /// <summary>
    /// Connects <paramref name="socket" /> to <paramref name="endPoint" /> through <see cref="ConnectAsync" />,
    /// and when the socket was bound to a local port (<c>--local-port</c>) throws a failure as a
    /// <see cref="BoundConnectFailedException" /> naming that end, as curl's failed-connect line does (BL-3306).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private async ValueTask<Socket> ConnectFromBoundEndAsync(Socket socket, IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        try
        {
            return await ConnectAsync(socket, endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception) when (socket.LocalEndPoint is IPEndPoint { Port: not 0 })
        {
            // The system gives a failed connect an ephemeral local port too, and curl names it (CGR-220).
            throw new BoundConnectFailedException(exception.SocketErrorCode, exception.Message, (IPEndPoint)socket.LocalEndPoint);
        }
    }

    /// <summary>
    /// Connects <paramref name="socket" /> to <paramref name="endPoint" />: through <c>connectx</c> on macOS
    /// when <see cref="FastOpenSocketOption.ConnectsThroughConnectx" /> says so (BL-1101), otherwise, or when
    /// <c>connectx</c> refuses, with <see cref="Socket.ConnectAsync(EndPoint, CancellationToken)" />.
    /// </summary>
    /// <returns>The connected socket, which replaces <paramref name="socket" /> after a <c>connectx</c>.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private async ValueTask<Socket> ConnectAsync(Socket socket, IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        if (FastOpenSocketOption.ConnectsThroughConnectx(SocketOptions, QualityOfServiceSocketOptions.CurrentPlatform)
            && DarwinFastOpenConnect.TryConnect(socket, endPoint) is { } connected)
        {
            return connected;
        }

        FailRefusedLoopbackAtOnce(socket, endPoint);
        await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
        return socket;
    }

    /// <summary>
    /// Gets whether Windows answers a refused connect to <paramref name="address" /> at once: only <c>127.0.0.1</c>
    /// and <c>::1</c> do. Any other address in <c>127.0.0.0/8</c> retransmits its SYN for about two seconds, as
    /// curl 8.22.0 sees it, so a short <c>--connect-timeout</c> is exit 28 there (CGR-189, BL-3718).
    /// </summary>
    /// <param name="address">The address connected to.</param>
    /// <returns><see langword="true" /> for <c>127.0.0.1</c> and <c>::1</c>.</returns>
    internal static bool RefusesAtOnceOnWindows(IPAddress address) =>
        address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback);

    /// <summary>
    /// On Windows, stops the SYN retransmissions of a connect to <c>127.0.0.1</c> or <c>::1</c> (SIO_TCP_INITIAL_RTO with
    /// no retransmissions), so a refused port fails at once with exit 7 as in curl 8.22.0 rather than after
    /// about two seconds, which a short <c>--connect-timeout</c> reports as exit 28 (BL-3182).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static void FailRefusedLoopbackAtOnce(Socket socket, IPEndPoint endPoint)
    {
        if (!OperatingSystem.IsWindows() || !RefusesAtOnceOnWindows(endPoint.Address))
        {
            return;
        }

        const int SioTcpInitialRto = unchecked((int)0x98000011);
        var parameters = new byte[] { 0xFF, 0xFF, 0xFE, 0x00 };
        try
        {
            socket.IOControl(SioTcpInitialRto, parameters, null);
        }
        catch (SocketException)
        {
            // Older Windows lacks the control: the connect then waits for the retransmissions as before.
        }
    }

    /// <summary>
    /// Adopts the operating system socket <paramref name="handle" /> an application supplied
    /// (<see cref="TcpSocketOpenDecision.Supplied" />, libcurl's <c>CURLOPT_OPENSOCKETFUNCTION</c>) without
    /// taking ownership, so closing the connection never closes the handle, and connects it to
    /// <paramref name="endPoint" /> unless <paramref name="alreadyConnected" /> (ADR-0903).
    /// </summary>
    /// <param name="handle">The socket handle.</param>
    /// <param name="alreadyConnected">Whether the socket is already connected.</param>
    /// <param name="endPoint">The address and port the attempt connects to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connection over the adopted socket.</returns>
    /// <exception cref="SocketException">The handle is no socket, or the connect failed.</exception>
    internal static async ValueTask<DialedTcpConnection> DialSuppliedSocketAsync(nint handle, bool alreadyConnected, IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        var socket = new Socket(new SafeSocketHandle(handle, ownsHandle: false));
        try
        {
            if (!alreadyConnected)
            {
                await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
            }

            return Connected(socket, endPoint);
        }
        catch (SocketException)
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps the dialed <paramref name="socket" />: in a <see cref="NetworkStream" /> when it is connected, or
    /// in a <see cref="DeferredConnectSocketStream" /> when <c>connectx</c> left its connect to the first write
    /// (BL-1158), as <see cref="NetworkStream" /> refuses a socket that is not yet connected. Either stream owns
    /// the socket, so disposing the connection closes it and the peer sees the close (AF-0222, BL-2275).
    /// </summary>
    internal static DialedTcpConnection Connected(Socket socket, IPEndPoint endPoint)
    {
        var localEndPoint = (IPEndPoint)socket.LocalEndPoint!;
        Stream stream = socket.Connected ? new NetworkStream(socket, ownsSocket: true) : new DeferredConnectSocketStream(socket);

        return new DialedTcpConnection(new StreamConnection(stream, endPoint, localEndPoint), localEndPoint);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Only <c>--mptcp</c> can fail here: a plain TCP socket opens wherever .NET runs, so no socket is
    /// opened to find out.
    /// </remarks>
    public SocketException? FailureToOpenSocket(AddressFamily family) =>
        SocketOptions.MultipathTcp ? TryOpenSocket(family, SocketOptions.SocketProtocol) : null;

    /// <summary>
    /// Opens and closes a stream socket of <paramref name="family" /> with <paramref name="protocol" />,
    /// giving the <see cref="SocketException" /> the operating system refuses it with, or
    /// <see langword="null" /> when it opens.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage per ADR-0083: which branch runs is the platform's, so the Windows coverage
    /// run, where Multipath TCP is refused, reaches only one.
    /// </remarks>
    /// <param name="family">The address family of the socket.</param>
    /// <param name="protocol">The protocol to open it with.</param>
    /// <returns>The refusal, or <see langword="null" />.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: the platform picks the branch.")]
    internal static SocketException? TryOpenSocket(AddressFamily family, ProtocolType protocol)
    {
        try
        {
            using var socket = new Socket(family, SocketType.Stream, protocol);
            return null;
        }
        catch (SocketException exception)
        {
            return exception;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Excluded from coverage per ADR-0083, as <see cref="DialAsync" /> is: every line after the
    /// argument check needs a listening socket, so the loopback test in the Integration run
    /// measures it. The TCP options are not set, as curl sets none on a Unix socket.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var endPoint = address.ToEndPoint();
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            PublishCreated(socket);
            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new StreamConnection(new NetworkStream(socket, ownsSocket: true), endPoint);
    }

    /// <summary>
    /// Binds <paramref name="socket" /> to the interface <paramref name="deviceName" /> with
    /// <c>setsockopt(SOL_SOCKET, SO_BINDTODEVICE)</c>, the name and its terminating NUL as libcurl's
    /// <c>bindlocal</c> passes them; Linux only, so every other platform answers <see langword="false" />.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage per ADR-0083: which branch runs is the platform's, so the Windows
    /// coverage run reaches only one. The Linux tests in <c>TcpDialerTests</c> pin both answers.
    /// Since Linux 5.7 an unprivileged process may bind an unbound socket to a device (measured
    /// with curl 8.18.0 as uid 1000, BL-1026 Notes); a name that is no device is <c>ENODEV</c>.
    /// </remarks>
    /// <param name="socket">A socket not yet bound or connected.</param>
    /// <param name="deviceName">The interface to bind to.</param>
    /// <returns><see langword="true" /> when the socket is bound to the device.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter whose branch the platform picks.")]
    internal static bool TryBindToDevice(Socket socket, string deviceName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            socket.SetRawSocketOption(LinuxSolSocket, LinuxSoBindToDevice, Encoding.UTF8.GetBytes(deviceName + '\0'));
            return true;
        }
        catch (SocketException)
        {
            // libcurl carries on to bind the interface's address, or the host's.
            return false;
        }
    }

    /// <summary>
    /// Binds <paramref name="socket" /> to <paramref name="localEndPoint" />'s address on the first
    /// port from <paramref name="localEndPoint" />'s port that binds, trying at most
    /// <paramref name="portCount" /> ports and never past 65535, as libcurl's <c>bindlocal</c> does.
    /// </summary>
    /// <param name="socket">A TCP or UDP socket not yet bound or connected.</param>
    /// <param name="localEndPoint">The local address and the first port.</param>
    /// <param name="portCount">How many ports to try; fewer than 1 is taken as 1.</param>
    /// <param name="events">
    /// Where curl's <c>-v</c> lines go (BL-1027): <c>Bind to local port N failed, trying next</c> for each port
    /// that would not bind with another left, then <c>Local port: N</c> for the port bound, as asked for, or
    /// <c>bind failed with errno N: reason</c> when none would.
    /// </param>
    /// <exception cref="LocalBindException">
    /// No port bound (<see cref="LocalBindFailure.InterfaceFailed" />): each was in use, not
    /// permitted, or the address is not local.
    /// </exception>
    internal static void BindLocalEnd(Socket socket, IPEndPoint localEndPoint, int portCount, ITransferEvents events)
    {
        var lastPort = Math.Min(localEndPoint.Port + Math.Max(portCount, 1) - 1, IPEndPoint.MaxPort);
        SocketException? lastFailure = null;
        for (var port = localEndPoint.Port; port <= lastPort; port++)
        {
            try
            {
                socket.Bind(new IPEndPoint(localEndPoint.Address, port));
                events.ReportInfo(LocalBindLines.LocalPort(port));
                return;
            }
            catch (SocketException exception)
            {
                // libcurl moves on to the next port of the range, saying so while one is left.
                lastFailure = exception;
                if (port < lastPort)
                {
                    events.ReportInfo(LocalBindLines.PortFailedTryingNext(port));
                }
            }
        }

        events.ReportInfo(LocalBindLines.BindFailed(lastFailure!, OperatingSystem.IsWindows()));
        throw new LocalBindException(LocalBindFailure.InterfaceFailed);
    }

    /// <summary>
    /// Sets <see cref="SocketOptions" /> on <paramref name="socket" />:<see cref="Socket.NoDelay" />
    /// from <see cref="TcpSocketOptions.NoDelay" />, the Type of Service or Traffic Class and priority
    /// <see cref="QualityOfServiceSocketOptions.For" /> lists, TCP Fast Open as <see cref="FastOpenSocketOption.For" />
    /// names it when <see cref="TcpSocketOptions.FastOpen" /> is set, and <c>SO_KEEPALIVE</c> from
    /// <see cref="TcpSocketOptions.KeepAlive" />, with the probe time and interval
    /// <see cref="TcpSocketOptions.KeepAliveSeconds" /> and the probe count
    /// <see cref="TcpSocketOptions.KeepAliveProbeCount" /> when it is on. An IPv6 socket is made dual-stack
    /// (<see cref="Socket.DualMode" />), so an IPv4-mapped address such as <c>[::ffff:127.0.0.1]</c> reaches an
    /// IPv4 listener, as curl 8.22.0 does (BL-3174, ADR-0778).
    /// </summary>
    /// <remarks>
    /// A timer the platform refuses is left at the platform's value and the connection goes ahead,
    /// as libcurl only notes such a failure in its verbose output: Linux, for one, refuses an idle
    /// time past 32767 seconds or more than 127 probes, and Windows more than 255 probes.
    /// </remarks>
    /// <param name="socket">A TCP socket not yet connected.</param>
    /// <param name="events">Where a refused <c>--vlan-priority</c> is warned of; none by default.</param>
    internal void ApplySocketOptions(Socket socket, ITransferEvents? events = null)
    {
        events ??= NoTransferEvents.Instance;
        socket.NoDelay = SocketOptions.NoDelay;
        if (socket.AddressFamily == AddressFamily.InterNetworkV6)
        {
            socket.DualMode = true;
        }

        var platform = QualityOfServiceSocketOptions.CurrentPlatform;
        foreach (RawSocketOption option in QualityOfServiceSocketOptions.For(SocketOptions, socket.AddressFamily, platform).Concat(FastOpenSocketOption.For(SocketOptions, platform)))
        {
            QualityOfServiceSocketOptions.TrySet(socket, option, events);
        }

        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, SocketOptions.KeepAlive);
        if (!SocketOptions.KeepAlive)
        {
            return;
        }

        TrySetTcpOption(socket, SocketOptionName.TcpKeepAliveTime, SocketOptions.KeepAliveSeconds);
        TrySetTcpOption(socket, SocketOptionName.TcpKeepAliveInterval, SocketOptions.KeepAliveSeconds);
        TrySetTcpOption(socket, SocketOptionName.TcpKeepAliveRetryCount, SocketOptions.KeepAliveProbeCount);
    }

    private static void TrySetTcpOption(Socket socket, SocketOptionName name, int value)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Tcp, name, value);
        }
        catch (SocketException)
        {
            // libcurl logs "Failed to set TCP_KEEP..." and connects anyway.
        }
    }
}
