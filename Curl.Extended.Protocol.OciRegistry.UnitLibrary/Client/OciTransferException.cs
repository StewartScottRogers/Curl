using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>A pull or push failed; <see cref="OciRegistryProtocolHandler"/> turns it into the transfer's exit code and message.</summary>
/// <param name="exitCode">The exit code curl gives the failure.</param>
/// <param name="message">What curl prints after <c>curl: (N)</c>.</param>
/// <param name="errorBody">The body of the registry's error response, or none when no error response was read.</param>
internal sealed class OciTransferException(CurlExitCode exitCode, string message, byte[]? errorBody = null) : Exception(message)
{
    /// <summary>Gets the exit code curl gives the failure.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>Gets the body of the registry's error response, which <c>--fail-with-body</c> writes; empty when no error response was read.</summary>
    public byte[] ErrorBody { get; } = errorBody ?? [];
}
