using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.Client;

/// <summary>A DNS query that failed, with the exit code curl ends with (ADR-1140).</summary>
/// <param name="exitCode">The exit code.</param>
/// <param name="message">The message curl prints.</param>
internal sealed class DnsQueryException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
