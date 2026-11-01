using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>A NATS transfer that failed, with the exit code curl ends with (ADR-1065).</summary>
/// <param name="exitCode">The exit code.</param>
/// <param name="message">The message curl prints.</param>
internal sealed class NatsException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
