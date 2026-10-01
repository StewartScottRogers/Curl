using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens one plaintext stream connection, to one TCP address or one Unix domain socket: the only
/// step of a connect that touches a socket, kept behind this seam so <see cref="TcpConnector" /> is
/// tested without a network.
/// </summary>
public interface ITcpDialer
{
    /// <summary>
    /// Connects to <paramref name="endPoint" />.
    /// </summary>
    /// <param name="endPoint">The address and port to connect to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection and the local end point of its socket.</returns>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken);

    /// <summary>
    /// Binds the local end to <paramref name="localEndPoint" />'s address on the first port of
    /// <paramref name="localEndPoint" />'s port to that port + <paramref name="localPortCount" /> - 1
    /// that binds, then connects to <paramref name="endPoint" /> (<c>--interface</c>, <c>--local-port</c>).
    /// </summary>
    /// <param name="endPoint">The address and port to connect to.</param>
    /// <param name="localEndPoint">The local address, of <paramref name="endPoint" />'s family, and the first local port (0 for any).</param>
    /// <param name="localPortCount">How many local ports to try, at least 1.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection and the local end point of its socket.</returns>
    /// <exception cref="LocalBindException">
    /// No port of the range could be bound (<see cref="LocalBindFailure.InterfaceFailed" />).
    /// </exception>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken);

    /// <summary>
    /// Binds the socket to the device <paramref name="deviceName" /> first, as libcurl's <c>bindlocal</c>
    /// does with <c>SO_BINDTODEVICE</c> on Linux (BL-1026), then, when that fails or
    /// <paramref name="bindsAddressAfterDevice" /> says so, binds the local end
    /// <paramref name="chooseLocalEndAsync" /> chooses on the first port of
    /// <paramref name="localPortCount" /> that binds, and connects to <paramref name="endPoint" />.
    /// </summary>
    /// <remarks>
    /// This default binds no device, as on every platform but Linux: it binds the local end chosen and
    /// connects through <see cref="DialFromAsync" />.
    /// </remarks>
    /// <param name="endPoint">The address and port to connect to.</param>
    /// <param name="deviceName">The interface to bind the socket to.</param>
    /// <param name="bindsAddressAfterDevice">
    /// <see langword="true" /> for <c>ifhost!</c>, whose host address is bound whether or not the device
    /// bind succeeded; <see langword="false" /> for a plain or <c>if!</c> name, whose device bind, when it
    /// succeeds, is the whole binding, ports included.
    /// </param>
    /// <param name="chooseLocalEndAsync">Chooses the local address and first port, only when one is to be bound.</param>
    /// <param name="localPortCount">How many local ports to try, at least 1.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection and the local end point of its socket.</returns>
    /// <exception cref="LocalBindException">
    /// No local end could be chosen, or no port of the range could be bound.
    /// </exception>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    async ValueTask<DialedTcpConnection> DialFromDeviceAsync(
        IPEndPoint endPoint,
        string deviceName,
        bool bindsAddressAfterDevice,
        Func<CancellationToken, ValueTask<IPEndPoint>> chooseLocalEndAsync,
        int localPortCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chooseLocalEndAsync);

        var localEndPoint = await chooseLocalEndAsync(cancellationToken).ConfigureAwait(false);
        return await DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to the Unix domain socket <paramref name="address" /> names (<c>--unix-socket</c>,
    /// <c>--abstract-unix-socket</c>).
    /// </summary>
    /// <param name="address">The socket to connect to.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The open plaintext connection.</returns>
    /// <exception cref="SocketException">The connection could not be made.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled.
    /// </exception>
    ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken);
}
