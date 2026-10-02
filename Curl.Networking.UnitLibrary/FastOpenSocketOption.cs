namespace Curl.Networking;

/// <summary>
/// The socket option <c>--tcp-fastopen</c> sets before the connect, in the operating system's own numbers:
/// <c>TCP_FASTOPEN</c> (15) on Windows, which <c>ConnectEx</c> honours; <c>TCP_FASTOPEN_CONNECT</c> (30) on
/// Linux, the route libcurl 8.21.0's <c>cf-socket.c</c> takes there; and <c>TCP_FASTOPEN</c> (<c>0x105</c>) on
/// macOS. The BCL names only Windows' portably, so each is set raw (ADR-0317).
/// </summary>
internal static class FastOpenSocketOption
{
    private const int IpProtocolTcp = 6;
    private const int WindowsTcpFastOpen = 15;
    private const int LinuxTcpFastOpenConnect = 30;
    private const int DarwinTcpFastOpen = 0x105;

    /// <summary>
    /// Lists the option that asks <paramref name="platform" /> for TCP Fast Open on a client socket when
    /// <see cref="TcpSocketOptions.FastOpen" /> is set: none when it is not, or where Curl knows of no option,
    /// as curl then connects as usual.
    /// </summary>
    /// <param name="options">The options the dialer was created with.</param>
    /// <param name="platform">The operating system whose numbers to use.</param>
    /// <returns>The option, set to 1, or nothing.</returns>
    internal static IReadOnlyList<RawSocketOption> For(TcpSocketOptions options, SocketPlatform platform) =>
        (options.FastOpen, platform) switch
        {
            (false, _) => [],
            (true, SocketPlatform.Windows) => [new RawSocketOption(IpProtocolTcp, WindowsTcpFastOpen, 1)],
            (true, SocketPlatform.Linux) => [new RawSocketOption(IpProtocolTcp, LinuxTcpFastOpenConnect, 1)],
            (true, SocketPlatform.Darwin) => [new RawSocketOption(IpProtocolTcp, DarwinTcpFastOpen, 1)],
            _ => [],
        };
}
