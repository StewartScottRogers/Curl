using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>Ends an MCP transfer with <see cref="ExitCode"/> and the exception's message as curl's error text.</summary>
/// <param name="exitCode">The exit code the transfer ends with.</param>
/// <param name="message">The error text, or <see langword="null"/> for none.</param>
internal sealed class McpClientException(CurlExitCode exitCode, string? message) : Exception(message)
{
    /// <summary>Gets the exit code the transfer ends with.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
