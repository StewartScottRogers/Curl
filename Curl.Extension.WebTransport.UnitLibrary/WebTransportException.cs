using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebTransport;

/// <summary>A WebTransport transfer that failed, with the exit code and message curl reports for it.</summary>
/// <param name="exitCode">The exit code.</param>
/// <param name="message">The message.</param>
internal sealed class WebTransportException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
