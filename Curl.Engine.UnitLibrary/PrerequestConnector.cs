using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Calls a handle's <c>CURLOPT_PREREQFUNCTION</c> each time a handler is given a connection, new or reused, before it
/// sends anything on it (ADR-0611): curl 8.22.0 calls it as each request enters its <c>DO</c> state, once the
/// connection is ready. A handler asks its connector once per request (each followed redirect again), so the callback
/// runs once per request. It is given the peer's and the local IP address and port as the connection reports them
/// (<see cref="IConnection.RemoteEndPoint" />, <see cref="IConnection.LocalEndPoint" />), an empty text and 0 for an
/// end point that is not an IP one. <see cref="PrerequestResult.Abort" /> (any answer but
/// <see cref="PrerequestResult.Ok" />) closes the connection and fails the connect with
/// <see cref="CurlExitCode.AbortedByCallback" /> and <see cref="AbortedText" />. A QUIC connection the handler opens
/// as a session is asked about too; a bare <see cref="ConnectMultiplexedAsync" /> passes through.
/// </summary>
/// <param name="inner">Gives the connections.</param>
/// <param name="prerequest">The handle's prerequest callback.</param>
internal sealed class PrerequestConnector(IConnector inner, PrerequestCallback prerequest) : IConnector
{
    /// <summary>curl 8.22.0's <c>failf</c> text when the prerequest callback refuses.</summary>
    internal const string AbortedText = "operation aborted by pre-request callback";

    /// <summary>
    /// Gives <paramref name="connector" /> wrapped in a <see cref="PrerequestConnector" /> when
    /// <paramref name="prerequest" /> is set, else <paramref name="connector" /> itself.
    /// </summary>
    /// <param name="connector">Gives the connections.</param>
    /// <param name="prerequest">The handle's prerequest callback, or <see langword="null" />.</param>
    /// <returns>The connector the handlers connect through.</returns>
    internal static IConnector Over(IConnector connector, PrerequestCallback? prerequest) =>
        prerequest is null ? connector : new PrerequestConnector(connector, prerequest);

    /// <summary>Connects through the inner connector, then asks the callback whether to go on.</summary>
    /// <param name="target">Where to connect.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The inner connector's result, or the abort.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        await AskAsync(await inner.ConnectAsync(target, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>Opens a QUIC connection as the inner connector does, without asking the callback.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Cancels the resolve and handshake.</param>
    /// <returns>The inner connector's result.</returns>
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        inner.ConnectMultiplexedAsync(target, cancellationToken);

    /// <summary>Gives a QUIC session as the inner connector does, then asks the callback whether to go on.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="openSession">Builds the session over a new QUIC connection.</param>
    /// <param name="cancellationToken">Cancels the resolve and handshake.</param>
    /// <returns>The inner connector's result, or the abort.</returns>
    public async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
        await AskAsync(await inner.ConnectMultiplexedSessionAsync(target, openSession, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>The text of <paramref name="endPoint" />'s address, or an empty text when it is not an IP end point.</summary>
    /// <param name="endPoint">The end point.</param>
    /// <returns>The address text.</returns>
    internal static string AddressTextOf(EndPoint? endPoint) => endPoint is IPEndPoint ip ? ip.Address.ToString() : string.Empty;

    /// <summary>The port of <paramref name="endPoint" />, or 0 when it is not an IP end point.</summary>
    /// <param name="endPoint">The end point.</param>
    /// <returns>The port.</returns>
    internal static int PortOf(EndPoint? endPoint) => endPoint is IPEndPoint ip ? ip.Port : 0;

    private async ValueTask<ConnectResult> AskAsync(ConnectResult connect)
    {
        if (connect.Connection is not { } connection)
        {
            return connect;
        }

        EndPoint? remote = connection.RemoteEndPoint;
        EndPoint? local = connection.LocalEndPoint;
        if (prerequest(AddressTextOf(remote), AddressTextOf(local), PortOf(remote), PortOf(local)) == PrerequestResult.Ok)
        {
            return connect;
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        return ConnectResult.Failed(CurlExitCode.AbortedByCallback, AbortedText);
    }
}
