namespace Curl.Networking;

/// <summary>
/// The socket options <see cref="TcpDialer" /> sets on every TCP socket it opens, as curl
/// sets them before it connects.
/// </summary>
/// <param name="NoDelay">
/// <see langword="true" />, curl's default, to set <c>TCP_NODELAY</c> and so send each write
/// at once; <see langword="false" /> for <c>--no-tcp-nodelay</c>, which leaves Nagle's
/// algorithm on.
/// </param>
/// <param name="KeepAlive">
/// <see langword="true" />, curl's default, to set <c>SO_KEEPALIVE</c> with the first probe
/// after <see cref="KeepAliveSeconds" /> idle seconds and the next ones that many seconds
/// apart; <see langword="false" /> for <c>--no-keepalive</c>, which sends no probes.
/// </param>
public sealed record TcpSocketOptions(bool NoDelay = true, bool KeepAlive = true)
{
    /// <summary>
    /// The idle time before the first keepalive probe and the interval between probes: 60
    /// seconds, curl's default for <c>--keepalive-time</c>.
    /// </summary>
    public const int KeepAliveSeconds = 60;
}
