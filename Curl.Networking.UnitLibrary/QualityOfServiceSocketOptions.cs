using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// One <c>setsockopt</c> call in the operating system's own numbers, for
/// <see cref="Socket.SetRawSocketOption" />.
/// </summary>
/// <param name="Level">The protocol level, such as <c>IPPROTO_IP</c>.</param>
/// <param name="Name">The option, such as <c>IP_TOS</c>.</param>
/// <param name="Value">The <c>int</c> the option is set to.</param>
internal readonly record struct RawSocketOption(int Level, int Name, int Value);

/// <summary>
/// The socket options <c>--ip-tos</c> and <c>--vlan-priority</c> ask for, as libcurl 8.21.0's
/// <c>cf-socket.c</c> sets them: <c>IP_TOS</c> on an IPv4 socket and <c>IPV6_TCLASS</c> on an IPv6 one
/// wherever the operating system defines them, and <c>SO_PRIORITY</c> where it defines that (Linux). The
/// BCL names none of these portably, so each is set raw, in the operating system's own numbers.
/// </summary>
internal static class QualityOfServiceSocketOptions
{
    private const int IpProtocolIp = 0;
    private const int IpProtocolIpV6 = 41;
    private const int LinuxSolSocket = 1;
    private const int LinuxSoPriority = 12;

    // IP_TOS on an IPv4 socket and IPV6_TCLASS on an IPv6 one, in each operating system's numbers.
    private static readonly Dictionary<(AddressFamily Family, SocketPlatform Platform), (int Level, int Name)> TypeOfServiceOptions = new()
    {
        [(AddressFamily.InterNetwork, SocketPlatform.Linux)] = (IpProtocolIp, 1),
        [(AddressFamily.InterNetwork, SocketPlatform.Windows)] = (IpProtocolIp, 3),
        [(AddressFamily.InterNetwork, SocketPlatform.Darwin)] = (IpProtocolIp, 3),
        [(AddressFamily.InterNetwork, SocketPlatform.FreeBsd)] = (IpProtocolIp, 3),
        [(AddressFamily.InterNetworkV6, SocketPlatform.Windows)] = (IpProtocolIpV6, 39),
        [(AddressFamily.InterNetworkV6, SocketPlatform.Linux)] = (IpProtocolIpV6, 67),
        [(AddressFamily.InterNetworkV6, SocketPlatform.Darwin)] = (IpProtocolIpV6, 36),
        [(AddressFamily.InterNetworkV6, SocketPlatform.FreeBsd)] = (IpProtocolIpV6, 61),
    };

    /// <summary>Gets the operating system this process runs on, as far as its socket option numbers go.</summary>
    /// <remarks>
    /// Excluded from coverage per ADR-0083: which branch runs is the platform's, so the Windows coverage
    /// run reaches only one.
    /// </remarks>
    internal static SocketPlatform CurrentPlatform
    {
        [ExcludeFromCodeCoverage(Justification = "ADR-0083: the platform picks the branch.")]
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return SocketPlatform.Windows;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
            {
                return SocketPlatform.Linux;
            }

            if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst())
            {
                return SocketPlatform.Darwin;
            }

            return OperatingSystem.IsFreeBSD() ? SocketPlatform.FreeBsd : SocketPlatform.Other;
        }
    }

    /// <summary>
    /// Lists the options to set on a socket of <paramref name="family" /> on <paramref name="platform" />:
    /// the Type of Service or Traffic Class when <see cref="TcpSocketOptions.TypeOfService" /> is above 0,
    /// then the priority when <see cref="TcpSocketOptions.VlanPriority" /> is above 0 and the platform has
    /// <c>SO_PRIORITY</c>.
    /// </summary>
    /// <param name="options">The options the dialer was created with.</param>
    /// <param name="family">The socket's address family.</param>
    /// <param name="platform">The operating system whose numbers to use.</param>
    /// <returns>The options, in the order to set them; empty when there is nothing to set.</returns>
    internal static IReadOnlyList<RawSocketOption> For(TcpSocketOptions options, AddressFamily family, SocketPlatform platform)
    {
        List<RawSocketOption> settings = [];
        if (options.TypeOfService > 0 && TypeOfServiceOption(family, platform) is (int level, int name))
        {
            settings.Add(new RawSocketOption(level, name, options.TypeOfService));
        }

        if (options.VlanPriority > 0 && platform == SocketPlatform.Linux)
        {
            settings.Add(new RawSocketOption(LinuxSolSocket, LinuxSoPriority, options.VlanPriority));
        }

        return settings;
    }

    private static (int Level, int Name)? TypeOfServiceOption(AddressFamily family, SocketPlatform platform) =>
        TypeOfServiceOptions.TryGetValue((family, platform), out var option) ? option : null;

    /// <summary>
    /// Sets <paramref name="option" /> on <paramref name="socket" />, leaving the socket as it was when the
    /// operating system refuses it, as libcurl only notes such a failure and connects anyway: Linux, for
    /// one, refuses a priority of 7 to a process without <c>CAP_NET_ADMIN</c>.
    /// </summary>
    /// <param name="socket">A socket not yet connected.</param>
    /// <param name="option">The option to set.</param>
    internal static void TrySet(Socket socket, RawSocketOption option)
    {
        try
        {
            socket.SetRawSocketOption(option.Level, option.Name, BitConverter.GetBytes(option.Value));
        }
        catch (SocketException)
        {
            // libcurl logs the failed setsockopt and connects anyway.
        }
    }
}
