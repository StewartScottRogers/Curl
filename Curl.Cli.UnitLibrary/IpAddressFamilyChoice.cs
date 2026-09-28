namespace Curl.Cli;

/// <summary>
/// Which IP address family a transfer may resolve a host name to and connect over, as the last
/// <c>-4</c> / <c>--ipv4</c> or <c>-6</c> / <c>--ipv6</c> on the command line chose it.
/// </summary>
public enum IpAddressFamilyChoice
{
    /// <summary>Neither option was given: use IPv4 or IPv6 addresses, whichever the name resolves to.</summary>
    Either = 0,

    /// <summary><c>-4</c> / <c>--ipv4</c> came last: use IPv4 addresses only.</summary>
    IPv4Only,

    /// <summary><c>-6</c> / <c>--ipv6</c> came last: use IPv6 addresses only.</summary>
    IPv6Only,
}
