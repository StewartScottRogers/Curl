namespace Curl.Networking;

/// <summary>
/// The operating systems whose numbers for <c>IP_TOS</c>, <c>IPV6_TCLASS</c> and <c>SO_PRIORITY</c> Curl knows, for <c>--ip-tos</c> and <c>--vlan-priority</c>.
/// </summary>
public enum SocketPlatform
{
    /// <summary>An operating system with none of the options, or one whose numbers are not known.</summary>
    Other,

    /// <summary>Windows: <c>IP_TOS</c> 3 and <c>IPV6_TCLASS</c> 39; no <c>SO_PRIORITY</c>.</summary>
    Windows,

    /// <summary>Linux: <c>IP_TOS</c> 1, <c>IPV6_TCLASS</c> 67 and <c>SO_PRIORITY</c> 12.</summary>
    Linux,

    /// <summary>macOS and the other Darwin systems: <c>IP_TOS</c> 3 and <c>IPV6_TCLASS</c> 36; no <c>SO_PRIORITY</c>.</summary>
    Darwin,

    /// <summary>FreeBSD: <c>IP_TOS</c> 3 and <c>IPV6_TCLASS</c> 61; no <c>SO_PRIORITY</c>.</summary>
    FreeBsd,
}
