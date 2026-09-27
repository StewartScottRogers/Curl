using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The part of a <see cref="ConnectionPoolKey" /> that names the proxy a connection tunnels
/// through: kind, host compared ignoring case, port, and the credential compared by user
/// name, password and domain rather than by reference.
/// </summary>
/// <param name="Kind">The proxy's kind.</param>
/// <param name="Host">The proxy's host, upper-cased so the comparison ignores case.</param>
/// <param name="Port">The proxy's port.</param>
/// <param name="UserName">The credential's user name, or <see langword="null" /> without one.</param>
/// <param name="Password">The credential's password, or <see langword="null" /> without one.</param>
/// <param name="Domain">The credential's domain, or <see langword="null" /> without one.</param>
internal sealed record ConnectionPoolProxyKey(
    ProxyKind Kind,
    string Host,
    int Port,
    string? UserName,
    string? Password,
    string? Domain)
{
    /// <summary>
    /// Builds the key of <paramref name="proxy" />, or <see langword="null" /> when there is
    /// no proxy.
    /// </summary>
    /// <param name="proxy">The target's <see cref="ConnectTarget.Proxy" />.</param>
    /// <returns>The key, or <see langword="null" /> for a direct connection.</returns>
    public static ConnectionPoolProxyKey? Of(ProxyEndpoint? proxy) =>
        proxy is null
            ? null
            : new ConnectionPoolProxyKey(
                proxy.Kind,
                proxy.Host.ToUpperInvariant(),
                proxy.Port,
                proxy.Credential?.UserName,
                proxy.Credential?.Password,
                proxy.Credential?.Domain);
}
