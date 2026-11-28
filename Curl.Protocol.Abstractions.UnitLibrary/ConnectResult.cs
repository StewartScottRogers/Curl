using System.Net;
using System.Net.Sockets;

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
    /// Gets whether a failed connect's last CONNECT reply was a <c>407</c> whose challenge
    /// offers the Basic or Digest scheme the CONNECT already sent, which curl gives up on with
    /// <c>&lt;scheme&gt; authentication problem, ignoring.</c> (curl's <c>authproblem</c>), so
    /// that under <c>-f</c> curl ends with exit 22 and only the generic text
    /// <c>HTTP response code said error</c> (measured, STD-227, ADR-1465).
    /// </summary>
    public bool IsProxyAuthenticationProblem { get; private set; }

    /// <summary>
    /// The key in an <see cref="OperationCanceledException" />'s <see cref="Exception.Data" /> whose
    /// presence says the TCP connection was open when the cancellation ended the connect, during the TLS
    /// handshake, which curl 8.22.0 still counts in <c>%{num_connects}</c> (measured, STD-231).
    /// </summary>
    public const string TcpConnectedOnCancellationKey = "Curl.ConnectResult.TcpConnected";

    /// <summary>
    /// Gets whether a failed connect had its TCP connection open when it failed, as a TLS handshake
    /// the connect limit ended has, which curl 8.22.0 still counts in <c>%{num_connects}</c>
    /// (measured, STD-231); <see langword="false" /> for a success and for a connect that never connected.
    /// </summary>
    public bool IsTcpConnectedBeforeFailure { get; private set; }

    /// <summary>
    /// Returns a copy of this result with <see cref="IsTcpConnectedBeforeFailure" /> set to
    /// <paramref name="isTcpConnectedBeforeFailure" />, every other property as it is.
    /// </summary>
    /// <param name="isTcpConnectedBeforeFailure">Whether the TCP connection was open when the connect failed.</param>
    /// <returns>The copy.</returns>
    public ConnectResult WithTcpConnectedBeforeFailure(bool isTcpConnectedBeforeFailure)
    {
        var copy = (ConnectResult)MemberwiseClone();
        copy.IsTcpConnectedBeforeFailure = isTcpConnectedBeforeFailure;
        return copy;
    }

    /// <summary>
    /// Gets the local end point of the TCP connection a failed connect had open when it failed, as a TLS
    /// handshake <c>-m</c> or the connect limit ended has; <see langword="null" /> when none was open or it is not known (EXT-097).
    /// </summary>
    public IPEndPoint? LocalEndPointBeforeFailure { get; private set; }

    /// <summary>
    /// Gets the peer of the TCP connection a failed connect had open when it failed, which an extended
    /// protocol reports as <c>%{remote_ip}</c> and <c>%{remote_port}</c>; <see langword="null" /> when none was open or it is not known (EXT-097).
    /// </summary>
    public IPEndPoint? RemoteEndPointBeforeFailure { get; private set; }

    /// <summary>
    /// Returns a copy of this result with <see cref="LocalEndPointBeforeFailure" /> and
    /// <see cref="RemoteEndPointBeforeFailure" /> set, every other property as it is.
    /// </summary>
    /// <param name="local">The local end point of the TCP connection open when the connect failed.</param>
    /// <param name="remote">The peer of the TCP connection open when the connect failed.</param>
    /// <returns>The copy.</returns>
    public ConnectResult WithEndPointsBeforeFailure(IPEndPoint? local, IPEndPoint? remote)
    {
        var copy = (ConnectResult)MemberwiseClone();
        copy.LocalEndPointBeforeFailure = local;
        copy.RemoteEndPointBeforeFailure = remote;
        return copy;
    }

    /// <summary>
    /// Gets the failed result an <see cref="OperationCanceledException" /> that ended a TLS handshake
    /// carries under <see cref="TcpConnectedOnCancellationKey" />, holding the open connection's end points;
    /// <see langword="null" /> when it carries none (EXT-097).
    /// </summary>
    /// <param name="canceled">The cancellation that ended the connect.</param>
    /// <returns>The carried result.</returns>
    public static ConnectResult? CarriedBy(OperationCanceledException canceled)
    {
        ArgumentNullException.ThrowIfNull(canceled);
        return canceled.Data[TcpConnectedOnCancellationKey] as ConnectResult;
    }

    /// <summary>
    /// Returns a copy of this result with <see cref="IsProxyAuthenticationProblem" /> set to
    /// <paramref name="isProxyAuthenticationProblem" />, every other property as it is.
    /// </summary>
    /// <param name="isProxyAuthenticationProblem">Whether the refused CONNECT drew curl's <c>authproblem</c>.</param>
    /// <returns>The copy.</returns>
    public ConnectResult WithProxyAuthenticationProblem(bool isProxyAuthenticationProblem)
    {
        var copy = (ConnectResult)MemberwiseClone();
        copy.IsProxyAuthenticationProblem = isProxyAuthenticationProblem;
        return copy;
    }

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
    /// Gets the socket error of the last connect attempt that failed, the source of libcurl's
    /// <c>CURLINFO_OS_ERRNO</c> (BL-2374), or of the earlier address's attempt that failed before a
    /// success connected (BL-3328); <see cref="SocketError.Success" /> for a success no attempt
    /// failed before and for a failure the connector built without one, such as a failed resolve.
    /// </summary>
    public SocketError LastSocketError { get; private init; }

    /// <summary>
    /// Gets why a SOCKS proxy handshake failed, libcurl's <c>CURLPX_*</c> code and the source of
    /// <c>CURLINFO_PROXY_ERROR</c> (BL-2375); <see cref="CurlProxyCode.Ok" /> for a success and for
    /// every failure that is not a SOCKS handshake's.
    /// </summary>
    public CurlProxyCode ProxyError { get; private init; }

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
    /// Gets how many header lines the reply to the CONNECT held, whether it opened a tunnel or the
    /// proxy refused it, which curl 8.21.0 stores and counts toward its limit of 5000 response
    /// headers (measured, BL-1609 Notes); <c>0</c> when there was no CONNECT, the connection was
    /// reused, or the connect failed before any CONNECT reply was read.
    /// </summary>
    public int ConnectReplyHeadersStored { get; private init; }

    /// <summary>
    /// Gets how many bytes the heads of the proxy's replies to the CONNECTs that opened a tunnel
    /// held, status lines and blank lines included and every reply counted, a <c>407</c> answered
    /// on the way too, which curl 8.21.0 adds to <c>%{size_header}</c> with or without
    /// <c>--suppress-connect-headers</c> (upstream test1288, BL-2010); a refused CONNECT reports its
    /// reply's bytes too; <c>0</c> when there was no CONNECT, the connection was reused, or the
    /// connect failed before any CONNECT reply was read.
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
    /// <param name="lastSocketError">
    /// The socket error of an earlier address's connect attempt that failed before this one
    /// connected, which curl keeps as <c>CURLINFO_OS_ERRNO</c> (BL-3328); <see cref="SocketError.Success" />
    /// when none failed.
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
        long proxyConnectHeaderBytes = 0,
        SocketError lastSocketError = SocketError.Success)
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
            LastSocketError = lastSocketError,
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
    /// <param name="lastSocketError">
    /// The socket error of the last connect attempt that failed; <see cref="SocketError.Success" />
    /// when the failure had none.
    /// </param>
    /// <param name="connectReplyHeadersStored">How many header lines the proxy's refusing reply to a CONNECT held; <c>0</c> when none.</param>
    /// <param name="proxyConnectHeaderBytes">How many bytes the heads of the proxy's replies to a CONNECT held, which curl still adds to <c>%{size_header}</c> when the CONNECT is refused (STD-144).</param>
    /// <param name="proxyError">
    /// Why the SOCKS proxy handshake failed; <see cref="CurlProxyCode.Ok" /> when the failure was
    /// not a SOCKS handshake's.
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
        int proxyConnectResponseCode = 0,
        SocketError lastSocketError = SocketError.Success,
        CurlProxyCode proxyError = CurlProxyCode.Ok,
        int connectReplyHeadersStored = 0,
        long proxyConnectHeaderBytes = 0)
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
            LastSocketError = lastSocketError,
            ProxyError = proxyError,
            ConnectReplyHeadersStored = connectReplyHeadersStored,
            ProxyConnectHeaderBytes = proxyConnectHeaderBytes,
        };
    }

    /// <summary>
    /// Creates the result of a TCP connect whose last attempt the peer refused
    /// (<c>ECONNREFUSED</c>): exit 7 with <see cref="IsConnectionRefused" /> set, the one
    /// exit 7 curl's <c>--retry-connrefused</c> retries, and <see cref="LastSocketError" />
    /// <see cref="SocketError.ConnectionRefused" />.
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
            LastSocketError = SocketError.ConnectionRefused,
            Timings = timings,
            ConnectionNumber = connectionNumber,
        };
}
