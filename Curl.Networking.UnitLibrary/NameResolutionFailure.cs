using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The exit code and message for a host or proxy name that did not resolve, shared by
/// <see cref="TcpConnector" /> and <see cref="UdpDatagramConnector" />: curl 8.21.0's
/// <c>Could not resolve host: &lt;host&gt;</c> (or <c>proxy</c>), followed, when the resolver said
/// why, by the reason in brackets as curl's c-ares build prints it, or exit 43
/// <c>Error 43 resolving &lt;host&gt;:&lt;port&gt;</c> when the c-ares options did not parse
/// (measured on curl 8.22.0 with c-ares 1.34.8, BL-643, BL-694).
/// </summary>
internal static class NameResolutionFailure
{
    /// <summary>Describes the failure to resolve <paramref name="host" />.</summary>
    /// <param name="notResolved">The exit code for a name that did not resolve: 6 for a host, 5 for a proxy.</param>
    /// <param name="subject">What the name is, <c>host</c> or <c>proxy</c>.</param>
    /// <param name="host">The name that did not resolve.</param>
    /// <param name="port">The port it was resolved for.</param>
    /// <param name="failure">Why, as the resolver reported it; <see cref="DnsLookupFailure.None" /> when it did not say.</param>
    /// <returns>The exit code and the message, cut to 255 characters as curl's error buffer cuts it.</returns>
    public static (CurlExitCode ExitCode, string Message) Describe(CurlExitCode notResolved, string subject, string host, int port, DnsLookupFailure failure)
    {
        if (failure == DnsLookupFailure.BadConfiguration)
        {
            return (CurlExitCode.BadFunctionArgument, CurlErrorBuffer.Truncate($"Error 43 resolving {host}:{port}"));
        }

        var reason = DnsLookupFailureText.Describe(failure);
        var suffix = reason.Length == 0 ? string.Empty : $" ({reason})";
        return (notResolved, CurlErrorBuffer.Truncate($"Could not resolve {subject}: {host}{suffix}"));
    }
}
