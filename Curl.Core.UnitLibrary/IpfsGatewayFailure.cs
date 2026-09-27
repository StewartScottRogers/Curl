using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Why <see cref="IpfsGatewayRewriter" /> could not rewrite an <c>ipfs://</c> or
/// <c>ipns://</c> URL: the exit code the transfer ends with and the message curl 8.21.0
/// prints for it.
/// </summary>
/// <remarks>
/// curl reports these from the tool, not from libcurl, so the message has no
/// <c>(&lt;code&gt;)</c>: the caller prints <c>curl: </c> and <see cref="Message" />, then
/// curl's <c>curl: try 'curl --help' or 'curl --manual' for more information</c> line.
/// </remarks>
/// <param name="ExitCode">The exit code the transfer ends with.</param>
/// <param name="Message">The message, without the <c>curl: </c> prefix.</param>
public sealed record IpfsGatewayFailure(CurlExitCode ExitCode, string Message)
{
    /// <summary>
    /// Gets the failure when no gateway is configured: no <c>--ipfs-gateway</c>, no
    /// <c>IPFS_GATEWAY</c> and no gateway file, or one whose first line is empty. Exit 37.
    /// </summary>
    public static IpfsGatewayFailure GatewayDetectionFailed { get; } =
        new(CurlExitCode.FileCouldntReadFile, "IPFS automatic gateway detection failed");

    /// <summary>
    /// Gets the failure when the gateway or the rewritten URL is not one curl accepts: a
    /// gateway with no scheme (from the environment or the file), an unknown scheme, no
    /// host, an IPv6 host or a query, or a path that decodes to a control character. Exit 3.
    /// </summary>
    public static IpfsGatewayFailure MalformedTargetUrl { get; } =
        new(CurlExitCode.UrlMalformat, "malformed target URL");
}
