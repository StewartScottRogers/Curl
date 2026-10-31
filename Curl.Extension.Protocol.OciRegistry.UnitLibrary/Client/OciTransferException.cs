using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.OciRegistry.Client;

/// <summary>A pull or push failed; <see cref="OciRegistryProtocolHandler"/> turns it into the transfer's exit code and message.</summary>
/// <param name="exitCode">The exit code curl gives the failure.</param>
/// <param name="message">What curl prints after <c>curl: (N)</c>.</param>
internal sealed class OciTransferException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code curl gives the failure.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
