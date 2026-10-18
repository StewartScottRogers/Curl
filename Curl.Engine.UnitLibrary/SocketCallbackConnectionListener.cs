using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Calls a handle's <c>CURLOPT_OPENSOCKETFUNCTION</c>, <c>CURLOPT_SOCKOPTFUNCTION</c> and
/// <c>CURLOPT_CLOSESOCKETFUNCTION</c> around the listening socket FTP's active mode (<c>CURLOPT_FTPPORT</c>) opens
/// through <paramref name="inner" />, as curl 8.22.0's <c>ftp_state_use_port</c> opens it through
/// <c>Curl_socket_open</c> (BL-2588): the open-socket callback is asked once the port is bound, and its
/// <see cref="CurlSocket.Bad" /> stops the listening and fails with <see cref="CurlExitCode.FtpPortFailed" /> and
/// <c>socket failure</c>; the close-socket callback is called once, when the peer's connection is accepted or, never
/// accepted, when the listening stops. The accepted connection is not the open and close callbacks' (curl's
/// <c>cf_socket_close</c> closes an accepted socket without them), but the socket-option callback gets it with
/// <see cref="CurlSocketType.Accept" />, as curl's <c>cf_tcp_accept_connect</c> calls it, and its
/// <see cref="SocketOptionResult.Error" /> closes it and fails the accept with
/// <see cref="CurlExitCode.AbortedByCallback" /> (upstream test 766, BL-2638).
/// </summary>
/// <param name="inner">Binds the listening ports.</param>
/// <param name="callbacks">The handle's socket callbacks.</param>
/// <param name="handles">Numbers the sockets the open-socket callback did not make.</param>
internal sealed class SocketCallbackConnectionListener(IConnectionListener inner, EasyCallbackOptions callbacks, CurlSocketHandles handles) : IConnectionListener
{
    /// <summary>The message a refused listening socket fails with, curl's <c>socket failure</c>.</summary>
    internal const string SocketFailureText = "socket failure";

    /// <summary>
    /// Gives <paramref name="listener" /> wrapped in a <see cref="SocketCallbackConnectionListener" /> when
    /// <paramref name="callbacks" /> sets an open-socket, socket-option or close-socket callback, else
    /// <paramref name="listener" /> itself.
    /// </summary>
    /// <param name="listener">Binds the listening ports.</param>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="shared">The engine's shared state, for its socket numbers.</param>
    /// <returns>The listener the transfer listens through.</returns>
    internal static IConnectionListener Over(IConnectionListener listener, EasyCallbackOptions callbacks, ManagedEngineSharedState shared) =>
        callbacks is { OpenSocket: null, SocketOption: null, CloseSocket: null }
            ? listener
            : new SocketCallbackConnectionListener(listener, callbacks, shared.SocketHandles);

    /// <summary>Binds through the inner listener, then runs the open-socket callback for the listening socket.</summary>
    /// <param name="target">The address and ports to bind.</param>
    /// <param name="cancellationToken">Cancels the bind.</param>
    /// <returns>The pending connection wrapped to run the callbacks at its accept, or the failure.</returns>
    public async ValueTask<ListenResult> ListenAsync(ListenTarget target, CancellationToken cancellationToken)
    {
        ListenResult listen = await inner.ListenAsync(target, cancellationToken).ConfigureAwait(false);
        if (listen.PendingConnection is not { } pending)
        {
            return listen;
        }

        CurlSocket socket = callbacks.OpenSocket is { } open
            ? open(CurlSocketType.IpConnection, SocketCallbackConnector.AddressOf(pending.LocalEndPoint))
            : handles.Next();
        if (socket == CurlSocket.Bad)
        {
            await pending.DisposeAsync().ConfigureAwait(false);
            return ListenResult.Failed(CurlExitCode.FtpPortFailed, SocketFailureText);
        }

        return ListenResult.Listening(new CallbackPendingConnection(pending, new CurlSocketCloser(socket, callbacks.CloseSocket), callbacks.SocketOption, handles));
    }

    /// <summary>
    /// A pending connection that closes the listening socket's callbacks' socket once it accepts or stops, and runs
    /// the socket-option callback for the accepted connection.
    /// </summary>
    /// <param name="inner">The inner listener's pending connection.</param>
    /// <param name="closer">Closes the socket the callbacks saw.</param>
    /// <param name="socketOption">The handle's socket-option callback, or <see langword="null" />.</param>
    /// <param name="handles">Numbers the accepted socket.</param>
    private sealed class CallbackPendingConnection(IPendingConnection inner, CurlSocketCloser closer, SocketOptionCallback? socketOption, CurlSocketHandles handles) : IPendingConnection
    {
        public EndPoint LocalEndPoint => inner.LocalEndPoint;

        public bool HasConnectionWaiting => inner.HasConnectionWaiting;

        public async ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken)
        {
            ConnectResult accepted = await inner.AcceptAsync(cancellationToken).ConfigureAwait(false);
            closer.Close();
            if (accepted.Connection is { } connection && socketOption?.Invoke(handles.Next(), CurlSocketType.Accept) == SocketOptionResult.Error)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return ConnectResult.Failed(CurlExitCode.AbortedByCallback, SocketCallbackConnector.AbortedByCallbackText);
            }

            return accepted;
        }

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            closer.Close();
        }
    }
}
