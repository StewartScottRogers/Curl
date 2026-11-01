using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Client;

/// <summary>A SignalR transfer failed with the exit code <see cref="ExitCode"/>: a bad <c>-d</c> message or an error the hub reported.</summary>
/// <param name="exitCode">The exit code curl ends with.</param>
/// <param name="message">The error message curl prints.</param>
internal sealed class SignalRHubException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code curl ends with.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
