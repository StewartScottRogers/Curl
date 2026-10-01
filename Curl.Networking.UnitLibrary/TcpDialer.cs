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
    /// Excluded from coverage per ADR-0083: every line after the argument check needs a
    /// connected TCP socket, so the loopback test in the Integration run measures it. The
    /// options it sets are <see cref="ApplySocketOptions" />'s, which unit tests measure.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        return DialBoundAsync(endPoint, null, 0, cancellationToken);
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

        return DialBoundAsync(endPoint, localEndPoint, localPortCount, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        ArgumentNullException.ThrowIfNull(deviceName);
        ArgumentNullException.ThrowIfNull(chooseLocalEndAsync);

        return DialDeviceBoundAsync(endPoint, deviceName, bindsAddressAfterDevice, chooseLocalEndAsync, localPortCount, cancellationToken);
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private async ValueTask<DialedTcpConnection> DialDeviceBoundAsync(
        IPEndPoint endPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            ApplySocketOptions(socket);
            if (!TryBindToDevice(socket, deviceName) || bindsAddressAfterDevice)
            {
                var localEndPoint = await chooseLocalEndAsync(cancellationToken).ConfigureAwait(false);
                BindLocalEnd(socket, localEndPoint, localPortCount);
            }

            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return Connected(socket, endPoint);
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private async ValueTask<DialedTcpConnection> DialBoundAsync(IPEndPoint endPoint, IPEndPoint? bindTo, int localPortCount, CancellationToken cancellationToken)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            ApplySocketOptions(socket);
            if (bindTo is not null)
            {
                BindLocalEnd(socket, bindTo, localPortCount);
            }

            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return Connected(socket, endPoint);
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter, measured by the Integration run.")]
    private static DialedTcpConnection Connected(Socket socket, IPEndPoint endPoint)
    {
        var localEndPoint = (IPEndPoint)socket.LocalEndPoint!;

        return new DialedTcpConnection(
            new StreamConnection(new NetworkStream(socket, ownsSocket: true), endPoint, localEndPoint),
            localEndPoint);
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
    /// <exception cref="LocalBindException">
    /// No port bound (<see cref="LocalBindFailure.InterfaceFailed" />): each was in use, not
    /// permitted, or the address is not local.
    /// </exception>
    internal static void BindLocalEnd(Socket socket, IPEndPoint localEndPoint, int portCount)
    {
        var lastPort = Math.Min(localEndPoint.Port + Math.Max(portCount, 1) - 1, IPEndPoint.MaxPort);
        for (var port = localEndPoint.Port; port <= lastPort; port++)
        {
            try
            {
                socket.Bind(new IPEndPoint(localEndPoint.Address, port));
                return;
            }
            catch (SocketException)
            {
                // libcurl moves on to the next port of the range.
            }
        }

        throw new LocalBindException(LocalBindFailure.InterfaceFailed);
    }

    /// <summary>
    /// Sets <see cref="SocketOptions" /> on <paramref name="socket" />:<see cref="Socket.NoDelay" />
    /// from <see cref="TcpSocketOptions.NoDelay" />, and <c>SO_KEEPALIVE</c> from
    /// <see cref="TcpSocketOptions.KeepAlive" />, with the probe time and interval
    /// <see cref="TcpSocketOptions.KeepAliveSeconds" /> and the probe count
    /// <see cref="TcpSocketOptions.KeepAliveProbeCount" /> when it is on.
    /// </summary>
    /// <remarks>
    /// A timer the platform refuses is left at the platform's value and the connection goes ahead,
    /// as libcurl only notes such a failure in its verbose output: Linux, for one, refuses an idle
    /// time past 32767 seconds or more than 127 probes, and Windows more than 255 probes.
    /// </remarks>
    /// <param name="socket">A TCP socket not yet connected.</param>
    internal void ApplySocketOptions(Socket socket)
    {
        socket.NoDelay = SocketOptions.NoDelay;
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
