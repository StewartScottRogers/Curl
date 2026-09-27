using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one <see cref="IConnector.ConnectAsync(ConnectTarget, CancellationToken)" />:
/// an open connection, or the curl exit code and message that say why there is none.
/// </summary>
/// <remarks>
/// A result is built only through the two <c>Connected</c> overloads and
/// <see cref="Failed(CurlExitCode, string)" />, so a success always carries a connection
/// and a failure always carries an exit code other than <see cref="CurlExitCode.Ok" />.
/// </remarks>
public sealed class ConnectResult
{
    private ConnectResult(IConnection? connection, CurlExitCode exitCode, string? errorMessage)
    {
        Connection = connection;
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the open connection, non-<see langword="null" /> exactly when
    /// <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />. Ownership passes to the
    /// caller, which disposes it.
    /// </summary>
    public IConnection? Connection { get; }

    /// <summary>
    /// Gets <see cref="CurlExitCode.Ok" /> for a success, or the curl exit code a failure
    /// carries, such as <see cref="CurlExitCode.CouldntResolveHost" /> (6) or
    /// <see cref="CurlExitCode.CouldntConnect" /> (7).
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>
    /// Gets the message curl prints for the failure, or <see langword="null" /> for a
    /// success.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the points in time the connector recorded while it connected, or
    /// <see langword="null" /> when it recorded none or the connect failed.
    /// </summary>
    public ConnectTimings? Timings { get; private init; }

    /// <summary>
    /// Gets the local address and port of the socket the connector opened, the source of
    /// <c>%{local_ip}</c> and <c>%{local_port}</c>; <see langword="null" /> when unknown or
    /// the connect failed.
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; private init; }

    /// <summary>
    /// Gets the status code of the proxy's reply to the CONNECT that opened a tunnel, the
    /// source of <c>%{http_connect}</c>; <c>0</c> when there was no CONNECT or the connect
    /// failed.
    /// </summary>
    public int ProxyConnectResponseCode { get; private init; }

    /// <summary>
    /// Creates the result of a successful connect that recorded no timings, endpoint or
    /// CONNECT code.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <returns>
    /// A result whose <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" /> and whose
    /// <see cref="ErrorMessage" />, <see cref="Timings" /> and <see cref="LocalEndPoint" />
    /// are <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection" /> is <see langword="null" />, which would leave a
    /// success with nothing to talk over.
    /// </exception>
    public static ConnectResult Connected(IConnection connection) =>
        Connected(connection, null);

    /// <summary>
    /// Creates the result of a successful connect, with what the connector measured.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="timings">The points in time the connector recorded, or <see langword="null" />.</param>
    /// <param name="localEndPoint">The local address and port of the socket, or <see langword="null" />.</param>
    /// <param name="proxyConnectResponseCode">
    /// The status code of the proxy's reply to a tunnelling CONNECT; <c>0</c> when there was none.
    /// </param>
    /// <returns>A result whose <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection" /> is <see langword="null" />, which would leave a
    /// success with nothing to talk over.
    /// </exception>
    public static ConnectResult Connected(
        IConnection connection,
        ConnectTimings? timings,
        IPEndPoint? localEndPoint = null,
        int proxyConnectResponseCode = 0)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return new ConnectResult(connection, CurlExitCode.Ok, null)
        {
            Timings = timings,
            LocalEndPoint = localEndPoint,
            ProxyConnectResponseCode = proxyConnectResponseCode,
        };
    }

    /// <summary>
    /// Creates the result of a failed resolve, connect or TLS handshake.
    /// </summary>
    /// <param name="exitCode">
    /// The curl exit code, such as <see cref="CurlExitCode.CouldntResolveHost" /> (6) or
    /// <see cref="CurlExitCode.CouldntConnect" /> (7).
    /// </param>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <returns>A result with no <see cref="Connection" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a
    /// failure; use <see cref="Connected(IConnection)" /> instead.
    /// </exception>
    public static ConnectResult Failed(CurlExitCode exitCode, string errorMessage)
    {
        if (exitCode == CurlExitCode.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exitCode),
                exitCode,
                "A failed connect cannot report CurlExitCode.Ok; use ConnectResult.Connected instead.");
        }

        return new ConnectResult(null, exitCode, errorMessage);
    }
}
