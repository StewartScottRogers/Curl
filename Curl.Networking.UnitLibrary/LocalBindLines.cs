using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// The <c>-v</c> lines libcurl's <c>bindlocal</c> writes while it binds a connection's local end for
/// <c>--interface</c> and <c>--local-port</c>, between <c>Trying</c> and the connect outcome (BL-1027;
/// measured with curl 8.21.0 on Windows and curl 8.18.0 on Linux, BL-1027 Notes).
/// </summary>
internal static class LocalBindLines
{
    /// <summary>
    /// <c>Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2</c>: the host bound, the family of
    /// the address dialled and the family of the first address the host resolved to, as the
    /// platform's <c>AF_*</c> numbers.
    /// </summary>
    /// <param name="hostName">The host name or address bound, as given.</param>
    /// <param name="dialledFamily">The family of the address dialled.</param>
    /// <param name="resolved">The first address <paramref name="hostName" /> resolved to.</param>
    /// <param name="onWindows">Whether to number the families as Windows does.</param>
    /// <param name="onLinux">Whether to number the families as Linux does; ignored on Windows.</param>
    /// <returns>The line.</returns>
    public static string NameResolved(string hostName, AddressFamily dialledFamily, IPAddress resolved, bool onWindows, bool onLinux) =>
        $"Name '{hostName}' family {FamilyNumber(dialledFamily, onWindows, onLinux)} resolved to '{resolved}' family {FamilyNumber(resolved.AddressFamily, onWindows, onLinux)}";

    /// <summary>
    /// The platform's <c>AF_INET</c> or <c>AF_INET6</c> number: 2 for IPv4 everywhere, and for IPv6
    /// 23 on Windows, 10 on Linux (both measured) and 30 on macOS (its <c>sys/socket.h</c>).
    /// </summary>
    /// <param name="family">The family.</param>
    /// <param name="onWindows">Whether to number it as Windows does.</param>
    /// <param name="onLinux">Whether to number it as Linux does; ignored on Windows.</param>
    /// <returns>The number.</returns>
    public static int FamilyNumber(AddressFamily family, bool onWindows, bool onLinux)
    {
        if (family != AddressFamily.InterNetworkV6)
        {
            return 2;
        }

        return onWindows ? 23 : onLinux ? 10 : 30;
    }

    /// <summary><c>Could not resolve host: bogus0</c>: the host to bind resolved to nothing.</summary>
    /// <param name="hostName">The host name bound.</param>
    /// <returns>The line.</returns>
    public static string CouldNotResolveHost(string hostName) => $"Could not resolve host: {hostName}";

    /// <summary>
    /// <c>Could not bind to 'bogus0' with errno 0: No error</c>, after <see cref="CouldNotResolveHost" />:
    /// errno 0 on Windows and, measured on Linux, errno 22 <c>Invalid argument</c> elsewhere.
    /// </summary>
    /// <param name="hostName">The host name bound.</param>
    /// <param name="onWindows">Whether to give the Windows build's errno.</param>
    /// <returns>The line.</returns>
    public static string CouldNotBindHost(string hostName, bool onWindows) =>
        $"Could not bind to '{hostName}' with errno {(onWindows ? "0: No error" : "22: Invalid argument")}";

    /// <summary>
    /// <c>Could not bind to interface 'Ethernet' with errno 0: No error</c>: an <c>if!</c> name that is no
    /// interface; errno 0 on Windows and, measured on Linux, the failed device bind's errno 19
    /// <c>No such device</c> elsewhere.
    /// </summary>
    /// <param name="interfaceName">The interface name bound.</param>
    /// <param name="onWindows">Whether to give the Windows build's errno.</param>
    /// <returns>The line.</returns>
    public static string CouldNotBindInterface(string interfaceName, bool onWindows) =>
        $"Could not bind to interface '{interfaceName}' with errno {(onWindows ? "0: No error" : "19: No such device")}";

    /// <summary>
    /// <c>socket successfully bound to interface 'lo'</c>: a plain or <c>if!</c> name bound as a device
    /// (Linux's <c>SO_BINDTODEVICE</c>), which binds no address or port after it.
    /// </summary>
    /// <param name="deviceName">The interface bound.</param>
    /// <returns>The line.</returns>
    public static string DeviceBound(string deviceName) => $"socket successfully bound to interface '{deviceName}'";

    /// <summary>
    /// <c>Local port: 40010</c>: the local end bound, naming the port asked for, so <c>0</c> when no
    /// <c>--local-port</c> was given.
    /// </summary>
    /// <param name="port">The port bound, as asked for.</param>
    /// <returns>The line.</returns>
    public static string LocalPort(int port) => $"Local port: {port}";

    /// <summary><c>Bind to local port 40000 failed, trying next</c>: a port of the range that would not bind, with another left.</summary>
    /// <param name="port">The port that would not bind.</param>
    /// <returns>The line.</returns>
    public static string PortFailedTryingNext(int port) => $"Bind to local port {port} failed, trying next";

    /// <summary>
    /// <c>bind failed with errno 10048: Address already in use</c>: the last port of the range would not
    /// bind either; the platform's error number and curl's words for it, as for a failed connect.
    /// </summary>
    /// <param name="exception">Why the last port would not bind.</param>
    /// <param name="onWindows">Whether to give the Windows build's words.</param>
    /// <returns>The line.</returns>
    public static string BindFailed(SocketException exception, bool onWindows) =>
        $"bind failed with errno {exception.NativeErrorCode}: {ConnectFailureReason.Describe(exception, onWindows)}";
}
