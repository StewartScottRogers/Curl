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
    /// Gets the failure when <c>--ipfs-gateway</c> is not a URL curl can parse: one it
    /// rejects outright, one with a scheme curl does not know, or one with no host and a
    /// scheme other than <c>file</c>. Exit 43, as curl 8.21.0 answers
    /// <c>--ipfs-gateway :::</c> and <c>--ipfs-gateway foo://h:1/</c> (measured 2026-09-27, BL-363).
    /// </summary>
    public static IpfsGatewayFailure MalformedGatewayOption { get; } =
        new(CurlExitCode.BadFunctionArgument, "--ipfs-gateway was given a malformed URL");

    /// <summary>
    /// Gets the failure when the gateway or the rewritten URL is not one curl accepts: a
    /// gateway from the environment or the file that is not a URL curl can parse (no
    /// scheme, an unknown scheme, no host), any gateway with an IPv6 host, a query or (a
    /// <c>file</c> gateway) no host, or a path that decodes to a control character. Exit 3.
    /// </summary>
    public static IpfsGatewayFailure MalformedTargetUrl { get; } =
        new(CurlExitCode.UrlMalformat, "malformed target URL");
}
