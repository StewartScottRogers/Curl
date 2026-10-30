using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>Why no token was issued for a challenge, with the exit code the transfer fails with.</summary>
/// <param name="exitCode">The exit code.</param>
/// <param name="message">The message curl prints.</param>
internal sealed class PrivacyPassException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the exit code the transfer fails with.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
