namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IConnector" /> is asked to connect to: a host, a port, and whether
/// to wrap the connection in TLS.
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
