namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IConnector" /> is asked to connect to: a host, a port, whether to
/// wrap the connection in TLS, and optionally the proxy to tunnel through (ADR-0014).
/// </summary>
/// <param name="Host">
/// The host name or address literal from the transfer's URL, or a host the protocol
/// negotiated mid-session, such as for FTP's data connection. Never empty or whitespace.
/// </param>
/// <param name="Port">The TCP port, from 1 to 65535.</param>
/// <param name="UseTls">
/// <see langword="true" /> to wrap the connection in TLS before it is returned, as a
/// secure scheme such as <c>ftps://</c> or <c>ldaps://</c> requires.
/// </param>
/// <remarks>
/// <see cref="Host" /> and <see cref="Port" /> are validated when the target is built and
/// have no <c>init</c> accessor, so a <c>with</c> expression cannot produce a target that
/// skipped the checks.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Host" /> is <see langword="null" />.</exception>
/// <exception cref="ArgumentException"><paramref name="Host" /> is empty or whitespace.</exception>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="Port" /> is less than 1 or greater than 65535.
/// </exception>
public sealed record ConnectTarget(string Host, int Port, bool UseTls)
{
    /// <summary>
    /// Gets the host to connect to; never empty or whitespace.
    /// </summary>
    public string Host { get; } = RequireHost(Host);

    /// <summary>
    /// Gets the TCP port to connect to, from 1 to 65535.
    /// </summary>
    public int Port { get; } = RequirePort(Port);

    /// <summary>
    /// Gets the proxy to tunnel through to <see cref="Host" /> and <see cref="Port" />, or
    /// <see langword="null" />, the default, to connect directly.
    /// </summary>
    /// <remarks>
    /// When set, the connector connects to the proxy, opens a tunnel to <see cref="Host" />
    /// and <see cref="Port" /> through it (CONNECT for the HTTP kinds, the SOCKS handshake
    /// for the SOCKS kinds), then applies TLS over the tunnel when <see cref="UseTls" /> is
    /// <see langword="true" />.
    /// </remarks>
    public ProxyEndpoint? Proxy { get; init; }

    /// <summary>
    /// Gets where the connector reports connection and TLS events, in the order they
    /// happen; <see cref="NoTransferEvents.Instance" />, the default, when nobody is
    /// listening.
    /// </summary>
    /// <remarks>
    /// A handler sets it from <see cref="ITransferContext.Events" />, so no connector
    /// learns about the transfer context (ADR-0046).
    /// </remarks>
    public ITransferEvents Events { get; init; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets the scheme that keys this target in a connection pool, such as <c>http</c> or
    /// <c>https</c>, or <see langword="null" />, the default, when the connection is never
    /// pooled and never served from a pool (ADR-0050).
    /// </summary>
    /// <remarks>
    /// Only a handler that hands connections back with <see cref="IConnection.MarkReusable" />
    /// sets it; every other protocol leaves it <see langword="null" />.
    /// </remarks>
    public string? PoolScheme { get; init; }

    private static string RequireHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host, nameof(Host));

        return host;
    }

    private static int RequirePort(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1, nameof(Port));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535, nameof(Port));

        return port;
    }
}
