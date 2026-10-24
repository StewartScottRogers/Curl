namespace Curl.Conformance.Differential.Protocols.Proxy;

/// <summary>
/// Runs proxy_server.py's <c>tunnel</c> after a proxy has answered: maps <c>localhost.test</c> to <c>127.0.0.1</c>,
/// connects through the <see cref="IProxyTunnelConnector"/>, and relays bytes both ways until either side ends.
/// An unreachable target ends the connection, as the script's failed <c>create_connection</c> did.
/// </summary>
internal static class ProxyTunnel
{
    private const string LocalHostName = "localhost.test";
    private const string LoopbackAddress = "127.0.0.1";

    public static async Task RelayAsync(Stream connection, IProxyTunnelConnector connector, string host, int port, CancellationToken cancellationToken)
    {
        using var remote = await connector.ConnectAsync(host == LocalHostName ? LoopbackAddress : host, port, cancellationToken);
        if (remote is null)
        {
            return;
        }

        var toRemote = connection.CopyToAsync(remote, cancellationToken);
        var toClient = remote.CopyToAsync(connection, cancellationToken);
        await Task.WhenAny(toRemote, toClient);
        remote.Dispose();
        await Task.WhenAll(toRemote, toClient).ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
}
