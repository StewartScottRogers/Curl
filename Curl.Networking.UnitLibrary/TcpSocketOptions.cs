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
/// after <see cref="KeepAliveSeconds" /> idle seconds, the next ones that many seconds apart,
/// and the connection given up after <see cref="KeepAliveProbeCount" /> unanswered probes;
/// <see langword="false" /> for <c>--no-keepalive</c>, which sends no probes.
/// </param>
/// <param name="KeepAliveSeconds">
/// The idle time before the first keepalive probe and the interval between probes, from
/// <c>--keepalive-time</c>: <see cref="DefaultKeepAliveSeconds" /> unless it says otherwise.
/// </param>
/// <param name="KeepAliveProbeCount">
/// How many unanswered keepalive probes end the connection, from <c>--keepalive-cnt</c>:
/// <see cref="DefaultKeepAliveProbeCount" /> unless it says otherwise.
/// </param>
/// <param name="TypeOfService">
/// The IPv4 Type of Service or IPv6 Traffic Class byte from <c>--ip-tos</c>, set as <c>IP_TOS</c> or
/// <c>IPV6_TCLASS</c>; 0, the default, sets neither, as curl passes libcurl only a value above 0.
/// </param>
/// <param name="VlanPriority">
/// The socket priority from <c>--vlan-priority</c>, set as <c>SO_PRIORITY</c> where the operating system
/// has it (Linux); 0, the default, sets nothing, as curl passes libcurl only a value above 0.
/// </param>
public sealed record TcpSocketOptions(
    bool NoDelay = true,
    bool KeepAlive = true,
    int KeepAliveSeconds = TcpSocketOptions.DefaultKeepAliveSeconds,
    int KeepAliveProbeCount = TcpSocketOptions.DefaultKeepAliveProbeCount,
    int TypeOfService = 0,
    int VlanPriority = 0)
{
    /// <summary>
    /// libcurl's idle time before the first keepalive probe and interval between probes: 60
    /// seconds, what curl uses when <c>--keepalive-time</c> is absent or 0.
    /// </summary>
    public const int DefaultKeepAliveSeconds = 60;

    /// <summary>
    /// libcurl's count of unanswered keepalive probes that end a connection: 9, what curl uses
    /// when <c>--keepalive-cnt</c> is absent or 0.
    /// </summary>
    public const int DefaultKeepAliveProbeCount = 9;

    /// <summary>
    /// Builds the options curl 8.21.0 sets from its command line: a <c>--keepalive-time</c> or
    /// <c>--keepalive-cnt</c> of 0 keeps libcurl's default, as curl passes libcurl only a value that
    /// is not 0, and a value past <see cref="int.MaxValue" /> is <see cref="int.MaxValue" />, as
    /// libcurl clamps it.
    /// </summary>
    /// <param name="noDelay">Whether <c>TCP_NODELAY</c> is set.</param>
    /// <param name="keepAlive">Whether <c>SO_KEEPALIVE</c> is set.</param>
    /// <param name="keepAliveSeconds">The <c>--keepalive-time</c> value, 0 or more; 0 when not given.</param>
    /// <param name="keepAliveProbeCount">The <c>--keepalive-cnt</c> value, 0 or more; 0 when not given.</param>
    /// <returns>The options to dial with.</returns>
    public static TcpSocketOptions FromCommandLine(bool noDelay, bool keepAlive, long keepAliveSeconds, long keepAliveProbeCount) =>
        new(
            noDelay,
            keepAlive,
            ValueOrDefault(keepAliveSeconds, DefaultKeepAliveSeconds),
            ValueOrDefault(keepAliveProbeCount, DefaultKeepAliveProbeCount));

    private static int ValueOrDefault(long value, int defaultValue) =>
        value == 0 ? defaultValue : (int)Math.Min(value, int.MaxValue);
}
