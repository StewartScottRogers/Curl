using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Where <see cref="ConnectToMappings.Map(string, int)" /> sends a connection: the host to
/// resolve and the port to dial, or why the matching <c>--connect-to</c> mapping did not parse.
/// </summary>
/// <param name="Host">The host to resolve; the URL's host when no mapping changed it.</param>
/// <param name="Port">The port to dial, 0 to 65535; the URL's port when no mapping changed it.</param>
/// <param name="IsMapped">
/// Whether a mapping matched, which makes curl's exit 7 message name this destination after
/// <c>via</c>.
/// </param>
/// <param name="ParseError">
/// curl's exit 49 message when the matching mapping's destination does not parse; otherwise
/// <see langword="null" />. When it is set, <paramref name="Host" />, <paramref name="Port" />
/// and <paramref name="IsMapped" /> carry no meaning.
/// </param>
public sealed record ConnectDestination(string Host, int Port, bool IsMapped, string? ParseError)
{
    /// <summary>
    /// Gets the exit code that goes with <see cref="ParseError" />: exit 49
    /// (<see cref="CurlExitCode.SetoptOptionSyntax" />) for a destination that does not parse,
    /// exit 4 (<see cref="CurlExitCode.NotBuiltIn" />) for an IPv6 literal while IPv6 is hidden
    /// (curl 8.22.0 <c>parse_connect_to_host_port</c> without <c>USE_IPV6</c>).
    /// </summary>
    public CurlExitCode ParseErrorCode { get; init; } = CurlExitCode.SetoptOptionSyntax;
}
