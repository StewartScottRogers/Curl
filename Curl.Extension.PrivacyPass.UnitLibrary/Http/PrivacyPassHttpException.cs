using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Http;

/// <summary>An exchange with an issuer failed; <see cref="PrivacyPassHttpClient"/> turns it into a failed <see cref="PrivacyPassHttpResult"/>.</summary>
/// <param name="failure">Why the exchange failed.</param>
/// <param name="exitCode">The exit code curl gives the same failure.</param>
/// <param name="message">A description of the failure.</param>
internal sealed class PrivacyPassHttpException(PrivacyPassHttpFailure failure, CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets why the exchange failed.</summary>
    public PrivacyPassHttpFailure Failure { get; } = failure;

    /// <summary>Gets the exit code curl gives the same failure.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>Creates the failure for a response that breaks HTTP/1.1's syntax, which curl reports as a weird server reply.</summary>
    /// <param name="failure">Which rule the response breaks.</param>
    /// <param name="message">A description of the failure.</param>
    /// <returns>The failure to throw.</returns>
    public static PrivacyPassHttpException Malformed(PrivacyPassHttpFailure failure, string message) =>
        new(failure, CurlExitCode.WeirdServerReply, message);

    /// <summary>Creates the failure for a connection that closed after the status line, before the response ended.</summary>
    /// <returns>The failure to throw.</returns>
    public static PrivacyPassHttpException ClosedEarly() =>
        new(PrivacyPassHttpFailure.ClosedEarly, CurlExitCode.PartialFile, "The connection closed before the response ended.");
}
