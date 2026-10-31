using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Plugin;

/// <summary>A helper broke the protocol of ADR-1142 or reported a failure; carries the exit code Curl reports.</summary>
/// <param name="exitCode">The exit code Curl reports.</param>
/// <param name="message">What went wrong.</param>
/// <param name="bytesTransferred">The body bytes written before it went wrong.</param>
internal sealed class HelperProtocolException(CurlExitCode exitCode, string message, long bytesTransferred = 0) : Exception(message)
{
    /// <summary>Gets the exit code Curl reports.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>Gets the body bytes written before it went wrong.</summary>
    public long BytesTransferred { get; } = bytesTransferred;
}
