namespace Curl.Protocol.Abstractions;

/// <summary>
/// Turns a <see cref="ConnectTarget" /> into an <see cref="IConnection" />: the seam a
/// byte-stream protocol handler acquires its transport through, once per transfer.
/// </summary>
/// <remarks>
/// <para>
/// A handler takes a connector in its constructor and asks it for a connection from the
/// host and port in each transfer's URL, because the handler is registered once and
/// exists before any URL does (ADR-0005). The handler owns and disposes the
/// <see cref="IConnection" /> it is given, and never constructs a
/// <see cref="System.Net.Sockets.Socket" /> or <c>SslStream</c> itself.
/// </para>
/// <para>
/// Every protocol but TFTP uses this seam; TFTP uses <see cref="IDatagramConnector" />.
/// A protocol that negotiates a second connection mid-session, such as FTP's data
/// connection, calls the same connector again with the negotiated host and port.
/// </para>
/// </remarks>
public interface IConnector
{
    /// <summary>
    /// Connects to <paramref name="target" />, wrapping the connection in TLS when
    /// <see cref="ConnectTarget.UseTls" /> is <see langword="true" />.
    /// </summary>
    /// <param name="target">The host, port and TLS choice to connect with.</param>
    /// <param name="cancellationToken">Cancels the resolve, connect and handshake.</param>
    /// <returns>
    /// <see cref="ConnectResult.Connected(IConnection)" /> with the open connection, or
    /// <see cref="ConnectResult.Failed(CurlExitCode, string)" /> carrying curl's exit code
    /// and the message curl prints: <see cref="CurlExitCode.CouldntResolveHost" /> (6)
    /// when the host does not resolve, <see cref="CurlExitCode.CouldntConnect" /> (7) when
    /// no connection can be made, and curl's TLS exit code when the handshake fails.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape; every resolve, connect or TLS failure is returned as
    /// a failed result instead.
    /// </exception>
    ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a QUIC connection to <paramref name="target" />, negotiating HTTP/3 in its
    /// TLS 1.3 handshake: the seam the HTTP handler takes for <c>--http3</c> and
    /// <c>--http3-only</c> (ADR-0144).
    /// </summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Cancels the resolve and handshake.</param>
    /// <returns>
    /// <see cref="MultiplexedConnectResult.Connected(IMultiplexedConnection, ConnectTimings?)" />
    /// with the open connection, or
    /// <see cref="MultiplexedConnectResult.Failed(CurlExitCode, string)" /> carrying curl's
    /// exit code and message. The default, for a connector with no QUIC, fails with
    /// <see cref="CurlExitCode.CouldntConnect" /> (7) and <c>QUIC is not available on this
    /// connector</c>, so every existing connector and test fake compiles unchanged;
    /// <c>Curl.Networking</c>'s connector overrides it.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape.
    /// </exception>
    ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        ValueTask.FromResult(MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "QUIC is not available on this connector"));

    /// <summary>
    /// Gives a connection over QUIC to <paramref name="target" /> as the session
    /// <paramref name="openSession" /> builds over it, such as the HTTP handler's HTTP/3 session,
    /// so a pooling connector can hand that one session to every transfer to the origin, each on
    /// a stream of its own, as curl multiplexes <c>-Z</c> transfers over HTTP/3 (BL-735).
    /// </summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="openSession">
    /// Builds the session over a new QUIC connection, which it then owns. A session that is also
    /// an <see cref="IConnectionSession" /> tells a pool how many transfers it carries at once.
    /// </param>
    /// <param name="cancellationToken">Cancels the resolve and handshake.</param>
    /// <returns>
    /// The session as the connect result's connection, or the failure of
    /// <see cref="ConnectMultiplexedAsync" />. The default opens a new QUIC connection every time
    /// (<see cref="MultiplexedConnectResult.ToConnectResult" />).
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape.
    /// </exception>
    async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(
        ConnectTarget target,
        Func<IMultiplexedConnection, IConnection> openSession,
        CancellationToken cancellationToken) =>
        (await ConnectMultiplexedAsync(target, cancellationToken).ConfigureAwait(false)).ToConnectResult(openSession);
}
