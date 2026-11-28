using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Connects through <paramref name="connector" /> and records each connection it opens with
/// <paramref name="recorder" />: the connect's <see cref="ConnectResult.LocalEndPoint" /> and
/// the connection's <see cref="IConnection.RemoteEndPoint" /> (ADR-0119).
/// </summary>
/// <param name="connector">Opens the connections.</param>
/// <param name="recorder">Remembers the first connection the transfer opens.</param>
internal sealed class EndPointRecordingConnector(IConnector connector, ConnectionEndPointRecorder recorder) : IConnector
{
    /// <summary>
    /// Connects as the wrapped connector does and records the connection it opened; a connect that
    /// succeeds after an earlier address failed also records that address's last socket error, and
    /// a failed connect records its last socket error and proxy error (BL-2374, BL-3328); either records the proxy
    /// CONNECT reply, the source of <c>%{http_connect}</c> and of the tunnel's part of <c>%{size_header}</c> and <c>%{num_headers}</c> (BL-5893, BL-6019);
    /// a connect that failed, or was cancelled, after its TCP connection opened is counted for <c>%{num_connects}</c> (STD-231).
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connect's result, unchanged.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ConnectResult connect;
        try
        {
            connect = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException canceled) when (canceled.Data.Contains(ConnectResult.TcpConnectedOnCancellationKey))
        {
            // -m ended a TLS handshake over an open TCP connection, which curl counts in %{num_connects} (STD-231).
            recorder.RecordFailedAfterOpening();
            throw;
        }

        if (connect.IsTcpConnectedBeforeFailure)
        {
            recorder.RecordFailedAfterOpening();
        }

        recorder.RecordProxyConnect(connect);
        if (connect.Connection is { } connection)
        {
            recorder.Record(connect.LocalEndPoint, connection.RemoteEndPoint, connect.IsReused, connect.Timings);
            recorder.RecordFailedConnect(connect.LastSocketError);
        }
        else
        {
            recorder.RecordFailedConnect(connect.LastSocketError, connect.ProxyError);
        }

        return connect;
    }

    /// <summary>
    /// Opens a QUIC connection as the wrapped connector does and records its end points;
    /// a failed connect records nothing.
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connect's result, unchanged.</returns>
    public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(target, cancellationToken).ConfigureAwait(false);
        if (connect.Connection is { } connection)
        {
            recorder.Record(connection.LocalEndPoint as IPEndPoint, connection.RemoteEndPoint as IPEndPoint);
        }

        return connect;
    }

    /// <summary>
    /// Opens the QUIC session through the wrapped connector, so its pool shares it, recording
    /// the endpoints of a new or reused one, or a failure's last socket error, as <see cref="ConnectAsync" /> does (BL-735).
    /// </summary>
    /// <param name="target">What to connect to.</param>
    /// <param name="openSession">Builds the session over a new QUIC connection.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The wrapped connector's result.</returns>
    public async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(
        ConnectTarget target,
        Func<IMultiplexedConnection, IConnection> openSession,
        CancellationToken cancellationToken)
    {
        ConnectResult connect = await connector.ConnectMultiplexedSessionAsync(target, openSession, cancellationToken).ConfigureAwait(false);
        recorder.RecordProxyConnect(connect);
        if (connect.Connection is { } connection)
        {
            recorder.Record(connect.LocalEndPoint, connection.RemoteEndPoint, connect.IsReused, connect.Timings);
        }
        else
        {
            recorder.RecordFailedConnect(connect.LastSocketError, connect.ProxyError);
        }

        return connect;
    }
}
