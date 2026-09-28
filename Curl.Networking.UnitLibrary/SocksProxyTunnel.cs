using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens a tunnel through a SOCKS proxy: runs the SOCKS4, SOCKS4a, SOCKS5 or SOCKS5h
/// handshake curl 8.21.0 runs (measured; the commands and bytes are in BL-213's Notes).
/// </summary>
/// <remarks>
/// Every read takes exactly the bytes the reply's layout says it holds, so no byte after
/// the handshake is taken off the connection: whatever follows belongs to the tunnel.
/// </remarks>
internal static class SocksProxyTunnel
{
    /// <summary>
    /// The exit 97 message when the proxy closes the connection before a reply is complete.
    /// </summary>
    public const string ProxyClosedMessage = "Failed to receive SOCKS response, proxy closed connection";

    /// <summary>
    /// Runs the handshake for <paramref name="proxy" />'s kind, asking the proxy to connect
    /// to <paramref name="host" /> and <paramref name="port" />.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="proxy">The proxy, whose kind picks the handshake and whose credential is presented.</param>
    /// <param name="host">The host the tunnel reaches, without brackets around an IPv6 literal.</param>
    /// <param name="port">The port the tunnel reaches.</param>
    /// <param name="resolve">Resolves <paramref name="host" /> when the kind resolves it locally (SOCKS4 and SOCKS5).</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>
    /// <see langword="null" /> when the tunnel is open; else the failure, exit 97
    /// (<see cref="CurlExitCode.Proxy" />) with curl's message, or exit 6 when the host does
    /// not resolve.
    /// </returns>
    public static ValueTask<ConnectResult?> OpenAsync(
        IConnection connection,
        ProxyEndpoint proxy,
        string host,
        int port,
        Func<string, int, CancellationToken, ValueTask<IReadOnlyList<IPAddress>>> resolve,
        CancellationToken cancellationToken) =>
        proxy.Kind is ProxyKind.Socks4 or ProxyKind.Socks4a
            ? Socks4Handshake.RunAsync(connection, proxy, host, port, resolve, cancellationToken)
            : Socks5Handshake.RunAsync(connection, proxy, host, port, resolve, cancellationToken);

    /// <summary>
    /// Reads exactly <paramref name="buffer" />'s length from the connection.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="buffer">Filled with the bytes read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true" /> when the buffer was filled; <see langword="false" /> when the proxy closed first.</returns>
    public static async ValueTask<bool> ReadExactlyAsync(IConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }

    /// <summary>
    /// Reads a reply of exactly <paramref name="length" /> bytes from the connection.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="length">The number of bytes the reply holds.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reply, or <see langword="null" /> when the proxy closed first.</returns>
    public static async ValueTask<byte[]?> ReadReplyAsync(IConnection connection, int length, CancellationToken cancellationToken)
    {
        var reply = new byte[length];
        return await ReadExactlyAsync(connection, reply, cancellationToken).ConfigureAwait(false) ? reply : null;
    }

    /// <summary>
    /// Writes <paramref name="bytes" /> to the proxy and flushes them.
    /// </summary>
    /// <param name="connection">The connection to the proxy.</param>
    /// <param name="bytes">The handshake message.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the bytes are flushed.</returns>
    public static async ValueTask SendAsync(IConnection connection, byte[] bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses <paramref name="host" /> as an IP address literal written the way it prints,
    /// as curl tells a literal from a name to resolve.
    /// </summary>
    /// <param name="host">The host, without brackets.</param>
    /// <returns>The address, or <see langword="null" /> when the host is a name.</returns>
    public static IPAddress? ParseAddressLiteral(string host) =>
        IPAddress.TryParse(host, out var address)
            && (address.AddressFamily == AddressFamily.InterNetworkV6 || address.ToString() == host)
            ? address
            : null;

    /// <summary>
    /// Creates the exit 97 failure with <paramref name="message" />.
    /// </summary>
    /// <param name="message">curl's message.</param>
    /// <returns>A failed <see cref="ConnectResult" />.</returns>
    public static ConnectResult? Failed(string message) => ConnectResult.Failed(CurlExitCode.Proxy, message);

    /// <summary>
    /// Creates the exit 6 failure for a host that does not resolve.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>A failed <see cref="ConnectResult" />.</returns>
    public static ConnectResult? CouldNotResolve(string host) =>
        ConnectResult.Failed(CurlExitCode.CouldntResolveHost, CurlErrorBuffer.Truncate($"Could not resolve host: {host}"));
}
