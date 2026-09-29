namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one
/// <see cref="IConnector.ConnectMultiplexedAsync(ConnectTarget, CancellationToken)" />: an
/// open QUIC connection, or the curl exit code and message that say why there is none
/// (ADR-0144).
/// </summary>
/// <remarks>
/// A result is built only through <see cref="Connected(IMultiplexedConnection, ConnectTimings?)" />
/// and <see cref="Failed(CurlExitCode, string)" />, so a success always carries a
/// connection and a failure always carries an exit code other than
/// <see cref="CurlExitCode.Ok" />. The HTTP handler reads it for <c>--http3</c> and
/// <c>--http3-only</c>.
/// </remarks>
public sealed class MultiplexedConnectResult
{
    private MultiplexedConnectResult(
        IMultiplexedConnection? connection,
        CurlExitCode exitCode,
        string? errorMessage,
        ConnectTimings? timings)
    {
        Connection = connection;
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
        Timings = timings;
    }

    /// <summary>
    /// Gets the open connection, non-<see langword="null" /> exactly when
    /// <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />. Ownership passes to the
    /// caller, which disposes it.
    /// </summary>
    public IMultiplexedConnection? Connection { get; }

    /// <summary>
    /// Gets <see cref="CurlExitCode.Ok" /> for a success, or the curl exit code a failure
    /// carries.
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>
    /// Gets the message curl prints for the failure, or <see langword="null" /> for a
    /// success.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the points in time the connector recorded, with the QUIC handshake's end as
    /// both <see cref="ConnectTimings.Connected" /> and
    /// <see cref="ConnectTimings.TlsHandshakeCompleted" />; <see langword="null" /> when it
    /// recorded none or the connect failed.
    /// </summary>
    public ConnectTimings? Timings { get; }

    /// <summary>
    /// Creates the result of a successful QUIC connect.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="timings">The points in time the connector recorded, or <see langword="null" />.</param>
    /// <returns>A result whose <see cref="ExitCode" /> is <see cref="CurlExitCode.Ok" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection" /> is <see langword="null" />.
    /// </exception>
    public static MultiplexedConnectResult Connected(IMultiplexedConnection connection, ConnectTimings? timings)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return new MultiplexedConnectResult(connection, CurlExitCode.Ok, null, timings);
    }

    /// <summary>
    /// Creates the result of a failed resolve or QUIC handshake.
    /// </summary>
    /// <param name="exitCode">
    /// The curl exit code, such as <see cref="CurlExitCode.CouldntConnect" /> (7) or
    /// <see cref="CurlExitCode.QuicConnectError" /> (96).
    /// </param>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <returns>A result with no <see cref="Connection" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a failure.
    /// </exception>
    public static MultiplexedConnectResult Failed(CurlExitCode exitCode, string errorMessage)
    {
        if (exitCode == CurlExitCode.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exitCode),
                exitCode,
                "A failed connect cannot report CurlExitCode.Ok; use MultiplexedConnectResult.Connected instead.");
        }

        return new MultiplexedConnectResult(null, exitCode, errorMessage, null);
    }
}
