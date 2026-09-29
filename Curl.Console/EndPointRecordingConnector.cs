using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

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
    /// Connects as the wrapped connector does and records the connection it opened;
    /// a failed connect records nothing.
    /// </summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connect's result, unchanged.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ConnectResult connect = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        if (connect.Connection is { } connection)
        {
            recorder.Record(connect.LocalEndPoint, connection.RemoteEndPoint as IPEndPoint);
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
}
