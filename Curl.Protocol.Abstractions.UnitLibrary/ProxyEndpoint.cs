namespace Curl.Protocol.Abstractions;

/// <summary>
/// The proxy already chosen for a URL: its kind, where it is, and the credential to
/// present to it (ADR-0014).
/// </summary>
/// <param name="Kind">The kind of proxy, which decides how a tunnel through it is opened.</param>
/// <param name="Host">The proxy's host name or address literal. Never empty or whitespace.</param>
/// <param name="Port">The proxy's TCP port, from 1 to 65535.</param>
/// <param name="Credential">
/// The user name and password from <c>-U</c>/<c>--proxy-user</c>, else from the proxy
/// URL's user information, or <see langword="null" /> when neither is present.
/// </param>
/// <remarks>
/// <see cref="Host" /> and <see cref="Port" /> are validated exactly as
/// <see cref="ConnectTarget" /> validates its own and have no <c>init</c> accessor, so a
/// <c>with</c> expression cannot produce an endpoint that skipped the checks.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Host" /> is <see langword="null" />.</exception>
/// <exception cref="ArgumentException"><paramref name="Host" /> is empty or whitespace.</exception>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="Port" /> is less than 1 or greater than 65535.
/// </exception>
public sealed record ProxyEndpoint(
    ProxyKind Kind,
    string Host,
    int Port,
    System.Net.NetworkCredential? Credential)
{
    /// <summary>
    /// Gets the proxy's host; never empty or whitespace.
    /// </summary>
    public string Host { get; } = RequireHost(Host);

    /// <summary>
    /// Gets the proxy's TCP port, from 1 to 65535.
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
