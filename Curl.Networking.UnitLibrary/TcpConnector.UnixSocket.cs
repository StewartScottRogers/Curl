using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// Dials <paramref name="unixSocket" /> in place of the target's host, port and proxy, with no
    /// name resolved, then runs TLS to the target's host when it asks for it, as curl 8.21.0 does
    /// (measured, BL-507). <c>-v</c> shows <c>  Trying &lt;name&gt;:0...</c>, where the name is
    /// <see cref="UnixSocketAddress.RemoteIpText" />, and a failed dial <c>Immediate connect fail for
    /// &lt;name&gt;: &lt;reason&gt;</c> and <c>connect to &lt;name&gt; port 0 from  port 0 failed:
    /// &lt;reason&gt;</c> before exit 7 <c>Failed to connect to &lt;host&gt;:&lt;port&gt; over
    /// unix://&lt;path&gt; after &lt;n&gt; ms: Could not connect to server</c>. A path too long for
    /// a <c>sockaddr_un</c> is exit 6 <c>Unix socket path too long: '&lt;path&gt;'</c>.
    /// </summary>
    private async ValueTask<ConnectResult> ConnectOverUnixSocketAsync(
        ConnectTarget target,
        UnixSocketAddress unixSocket,
        long started,
        CancellationToken cancellationToken)
    {
        if (unixSocket.IsTooLong(OperatingSystem.IsMacOS()))
        {
            return ConnectResult.Failed(
                CurlExitCode.CouldntResolveHost,
                CurlErrorBuffer.Truncate($"Unix socket path too long: '{unixSocket.Path}'"));
        }

        // Over a Unix socket the setup filter eyeballs to the socket's path, port 0, and an https://
        // origin's ALPN connect filter goes around it as for a direct connect (measured, BL-1254 Notes).
        var httpsOrigin = IsHttpsOrigin(target);
        var (events, httpsConnect) = SetupAndDnsFilterEvents(
            target.Events,
            httpsOrigin,
            TracesSetupFilter,
            below => DnsFilterTraceEvents.StartOverUnixSocket(below, unixSocket.Path),
            below => new SetupFilterTraceEvents(below, unixSocket.Path, 0));
        var result = await ConnectOverUnixSocketTracedAsync(target with { Events = events }, unixSocket, started, cancellationToken).ConfigureAwait(false);
        ReportHttpsConnectFailure(httpsConnect, result);
        return result;
    }

    private async ValueTask<ConnectResult> ConnectOverUnixSocketTracedAsync(
        ConnectTarget target,
        UnixSocketAddress unixSocket,
        long started,
        CancellationToken cancellationToken)
    {
        var name = unixSocket.RemoteIpText;
        target.Events.ReportInfo($"  Trying {name}:0...");
        var nameResolved = timeProvider.GetTimestamp();
        IConnection connection;
        try
        {
            connection = await tcpDialer.DialUnixSocketAsync(unixSocket, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            var reason = ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows());
            target.Events.ReportInfo($"Immediate connect fail for {name}: {reason}");
            target.Events.ReportInfo($"connect to {name} port 0 from  port 0 failed: {reason}");
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(nameResolved).TotalMilliseconds;
            return DialFailure(
                target.Events,
                exception.SocketErrorCode,
                null,
                new ConnectTimings(started, nameResolved, null, null),
                CurlErrorBuffer.Truncate($"Failed to connect to {target.Host}:{target.Port} over unix://{unixSocket.Path} after {elapsedMilliseconds} ms: Could not connect to server"));
        }

        // A refused dial reaches the diagnostic log at error through ConnectWithinAsync (BL-968).
        new NetworkDiagnosticLog(target.DiagnosticLog).UnixSocketConnected(unixSocket.Path);
        var dialed = new DialedSocket(connection, null, unixSocket.Path, null, name, unixSocket.Path);
        var timings = new ConnectTimings(started, nameResolved, timeProvider.GetTimestamp(), null);
        return await SecureWhenAskedAsync(dialed, target, timings, 0, cancellationToken, WritesSslFilterAddedFor(target)).ConfigureAwait(false);
    }

    /// <summary>
    /// Dials the Unix domain socket a SOCKS proxy at <c>localhost</c> names in its path
    /// (<see cref="ProxyEndpoint.UnixSocketPath" />, BL-2955) in place of resolving and dialling its
    /// host and port, writing <c>Trying &lt;name&gt;:0...</c> as a <c>--unix-socket</c> dial does; a
    /// refused dial gives its <see cref="SocketError" />, which the caller turns into exit 7.
    /// </summary>
    private async ValueTask<(DialedSocket? Dialed, SocketError LastError, LocalBindFailure? LastBindFailure)> DialProxyUnixSocketAsync(
        string socketPath,
        string hostName,
        ConnectTarget target,
        CancellationToken cancellationToken)
    {
        var unixSocket = new UnixSocketAddress(socketPath, IsAbstract: false);
        var name = unixSocket.RemoteIpText;
        target.Events.ReportInfo($"  Trying {name}:0...");
        try
        {
            var connection = await tcpDialer.DialUnixSocketAsync(unixSocket, cancellationToken).ConfigureAwait(false);
            return (new DialedSocket(connection, null, hostName, null, name), SocketError.Success, null);
        }
        catch (SocketException exception)
        {
            return (null, exception.SocketErrorCode, null);
        }
    }
}
