using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one <see cref="IConnector.ConnectAsync(ConnectTarget, CancellationToken)" />:
/// an open connection, or the curl exit code and message that say why there is none.
/// </summary>
/// <remarks>
/// A result is built only through the two <c>Connected</c> overloads and the two
/// <c>Failed</c> and two <c>Refused</c> overloads, so a success always carries a connection
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
    /// <see langword="null" /> when it recorded none. A failed connect carries them only
    /// when built with timings, as <c>TcpConnector</c> builds a failed dial (ADR-0091).
    /// </summary>
    public ConnectTimings? Timings { get; private init; }

    /// <summary>
    /// Gets the local address and port of the socket the connector opened, the source of
    /// <c>%{local_ip}</c> and <c>%{local_port}</c>; <see langword="null" /> when unknown or
    /// the connect failed.
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; private init; }

    /// <summary>
    /// Gets the status code of the proxy's reply to the CONNECT that opened a tunnel, or of a
    /// failed connect's last CONNECT reply, the source of <c>%{http_connect}</c>; <c>0</c> when
    /// there was no CONNECT or no reply status.
    /// </summary>
    public int ProxyConnectResponseCode { get; private init; }

    /// <summary>
    /// Gets the DER encoding of every certificate the server sent in the TLS handshake,
    /// its own certificate first and the rest in the order sent, the source of
    /// <c>%{certs}</c> and <c>%{num_certs}</c> (ADR-0054); empty for a connection without
    /// TLS or a failed connect.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> PeerCertificates { get; private init; } = [];

    /// <summary>
    /// Gets a value indicating whether the connection was taken from a pool rather than
    /// opened for this connect, which makes the transfer's <c>%{num_connects}</c> <c>0</c>
    /// (ADR-0050); <see langword="false" /> for a new connection or a failed connect.
    /// </summary>
    public bool IsReused { get; private init; }

    /// <summary>
    /// Gets curl's number for the connection, the <c>N</c> of <c>#N</c> in <c>-v</c>,
    /// counted from <c>0</c> in the order connections are opened (ADR-0050); <c>0</c> when
    /// the connector does not number connections. A failed connect carries the number curl
    /// gave the connection it tried, as curl 8.21.0 numbers failed connections too (ADR-0109).
    /// </summary>
    public long ConnectionNumber { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the connect failed because the peer refused the last
    /// address tried (<c>ECONNREFUSED</c>, curl's <c>CURLINFO_OS_ERRNO</c>), the only
    /// exit 7 <c>--retry-connrefused</c> retries; <see langword="false" /> for a success
    /// and for every other failure. Set only by <see cref="Refused(string)" />.
    /// </summary>
    public bool IsConnectionRefused { get; private init; }

    /// <summary>
    /// Gets the application protocol the TLS handshake agreed with ALPN, such as <c>h2</c> or
    /// <c>http/1.1</c>; <see langword="null" /> when none was agreed, for a connection without
    /// TLS, and for a failed connect. <c>h2</c> makes the HTTP handler speak HTTP/2 (ADR-0141).
    /// </summary>
    public string? ApplicationProtocol { get; private init; }

    /// <summary>
    /// Gets the path of the Unix domain socket the connection was dialled through in place of
    /// the target's host and port (<c>--unix-socket</c>, or the name <c>--abstract-unix-socket</c>
    /// gave); <see langword="null" /> for a TCP connection and for a failed connect. curl 8.21.0
    /// names it, lower-cased, with port <c>0</c> in its <c>left intact</c> line (BL-794).
    /// </summary>
    public string? UnixSocketPath { get; private init; }

    /// <summary>
    /// Gets the host a <c>--connect-to</c> mapping, or an alt-svc alternative, sent the
    /// connection to in place of the target's host; <see langword="null" /> when it went to the
    /// target's own host, through a proxy, over a Unix domain socket, or the connect failed.
    /// curl 8.21.0 names it, with <see cref="MappedPort" />, in its <c>left intact</c> line
    /// (measured, BL-975).
    /// </summary>
    public string? MappedHost { get; private init; }

    /// <summary>
    /// Gets the port beside <see cref="MappedHost" />; <c>0</c> when <see cref="MappedHost" /> is
    /// <see langword="null" />.
    /// </summary>
    public int MappedPort { get; private init; }

    /// <summary>
    /// Gets how many header lines the reply to the CONNECT that opened a tunnel held, which curl
    /// 8.21.0 stores and counts toward its limit of 5000 response headers (measured, BL-1609 Notes);
    /// <c>0</c> when there was no CONNECT, the connection was reused, or the connect failed.
    /// </summary>
    public int ConnectReplyHeadersStored { get; private init; }

    /// <summary>
    /// Gets how many bytes the heads of the proxy's replies to the CONNECTs that opened a tunnel
    /// held, status lines and blank lines included and every reply counted, a <c>407</c> answered
    /// on the way too, which curl 8.21.0 adds to <c>%{size_header}</c> with or without
    /// <c>--suppress-connect-headers</c> (upstream test1288, BL-2010); <c>0</c> when there was no
    /// CONNECT, the connection was reused, or the connect failed.
    /// </summary>
    public long ProxyConnectHeaderBytes { get; private init; }

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
    /// <param name="peerCertificates">
    /// The DER encoding of every certificate the server sent, its own first; <see langword="null" />
    /// or empty when there was no TLS handshake.
    /// </param>
    /// <param name="isReused">
    /// <see langword="true" /> when the connection was taken from a pool rather than opened.
    /// </param>
    /// <param name="connectionNumber">
    /// curl's number for the connection, counted from <c>0</c>; <c>0</c> when not numbered.
    /// </param>
    /// <param name="applicationProtocol">
    /// The application protocol the TLS handshake agreed with ALPN, or <see langword="null" />
    /// when none was agreed.
    /// </param>
    /// <param name="unixSocketPath">
    /// The path of the Unix domain socket the connection was dialled through, or
    /// <see langword="null" /> for a TCP connection.
    /// </param>
    /// <param name="mappedHost">
    /// The host a <c>--connect-to</c> mapping or alt-svc alternative sent the connection to, or
    /// <see langword="null" /> when it went to the target's own host.
    /// </param>
    /// <param name="mappedPort">The port beside <paramref name="mappedHost" />; <c>0</c> when there is none.</param>
    /// <param name="connectReplyHeadersStored">
    /// How many header lines the reply to a tunnelling CONNECT held; <c>0</c> when there was none.
    /// </param>
    /// <param name="proxyConnectHeaderBytes">
    /// How many bytes the heads of the proxy's replies to a tunnelling CONNECT held; <c>0</c> when
    /// there was none.
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
        int proxyConnectResponseCode = 0,
        IReadOnlyList<ReadOnlyMemory<byte>>? peerCertificates = null,
        bool isReused = false,
        long connectionNumber = 0,
        string? applicationProtocol = null,
        string? unixSocketPath = null,
        string? mappedHost = null,
        int mappedPort = 0,
        int connectReplyHeadersStored = 0,
        long proxyConnectHeaderBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return new ConnectResult(connection, CurlExitCode.Ok, null)
        {
            Timings = timings,
            LocalEndPoint = localEndPoint,
            ProxyConnectResponseCode = proxyConnectResponseCode,
            PeerCertificates = peerCertificates ?? [],
            IsReused = isReused,
            ConnectionNumber = connectionNumber,
            ApplicationProtocol = applicationProtocol,
            UnixSocketPath = unixSocketPath,
            MappedHost = mappedHost,
            MappedPort = mappedPort,
            ConnectReplyHeadersStored = connectReplyHeadersStored,
            ProxyConnectHeaderBytes = proxyConnectHeaderBytes,
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
    public static ConnectResult Failed(CurlExitCode exitCode, string errorMessage) =>
        Failed(exitCode, errorMessage, null);

    /// <summary>
    /// Creates the result of a failed resolve, connect or TLS handshake, with the points in
    /// time the connector recorded before it failed.
    /// </summary>
    /// <param name="exitCode">
    /// The curl exit code, such as <see cref="CurlExitCode.CouldntResolveHost" /> (6) or
    /// <see cref="CurlExitCode.CouldntConnect" /> (7).
    /// </param>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <param name="timings">
    /// The points in time recorded before the failure, with <see cref="ConnectTimings.Connected" />
    /// <see langword="null" />; or <see langword="null" /> when none were recorded.
    /// </param>
    /// <param name="connectionNumber">
    /// curl's number for the connection the connect tried, counted from <c>0</c>; <c>0</c>
    /// when not numbered.
    /// </param>
    /// <param name="proxyConnectResponseCode">
    /// The status code of the proxy's reply to a CONNECT that did not open the tunnel, which
    /// curl 8.21.0 still reports as <c>%{http_connect}</c> (BL-1857); <c>0</c> when none.
    /// </param>
    /// <returns>A result with no <see cref="Connection" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="exitCode" /> is <see cref="CurlExitCode.Ok" />, which is not a
    /// failure; use <see cref="Connected(IConnection)" /> instead.
    /// </exception>
    public static ConnectResult Failed(
        CurlExitCode exitCode,
        string errorMessage,
        ConnectTimings? timings,
        long connectionNumber = 0,
        int proxyConnectResponseCode = 0)
    {
        if (exitCode == CurlExitCode.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exitCode),
                exitCode,
                "A failed connect cannot report CurlExitCode.Ok; use ConnectResult.Connected instead.");
        }

        return new ConnectResult(null, exitCode, errorMessage)
        {
            Timings = timings,
            ConnectionNumber = connectionNumber,
            ProxyConnectResponseCode = proxyConnectResponseCode,
        };
    }

    /// <summary>
    /// Creates the result of a TCP connect whose last attempt the peer refused
    /// (<c>ECONNREFUSED</c>): exit 7 with <see cref="IsConnectionRefused" /> set, the one
    /// exit 7 curl's <c>--retry-connrefused</c> retries.
    /// </summary>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <returns>
    /// A result with no <see cref="Connection" /> whose <see cref="ExitCode" /> is
    /// <see cref="CurlExitCode.CouldntConnect" />.
    /// </returns>
    public static ConnectResult Refused(string errorMessage) =>
        Refused(errorMessage, null);

    /// <summary>
    /// Creates the result of a TCP connect whose last attempt the peer refused, as
    /// <see cref="Refused(string)" />, with the points in time the connector recorded before
    /// the dial failed.
    /// </summary>
    /// <param name="errorMessage">The message curl prints for the failure.</param>
    /// <param name="timings">
    /// The points in time recorded before the failure, with <see cref="ConnectTimings.Connected" />
    /// <see langword="null" />; or <see langword="null" /> when none were recorded.
    /// </param>
    /// <param name="connectionNumber">
    /// curl's number for the connection the connect tried, counted from <c>0</c>; <c>0</c>
    /// when not numbered.
    /// </param>
    /// <returns>
    /// A result with no <see cref="Connection" /> whose <see cref="ExitCode" /> is
    /// <see cref="CurlExitCode.CouldntConnect" />.
    /// </returns>
    public static ConnectResult Refused(string errorMessage, ConnectTimings? timings, long connectionNumber = 0) =>
        new(null, CurlExitCode.CouldntConnect, errorMessage)
        {
            IsConnectionRefused = true,
            Timings = timings,
            ConnectionNumber = connectionNumber,
        };
}
