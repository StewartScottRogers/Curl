using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Http;

/// <summary>An exchange with an endpoint failed; <see cref="OAuth2HttpClient"/> turns it into a failed <see cref="OAuth2HttpResult"/>.</summary>
/// <param name="failure">Why the exchange failed.</param>
/// <param name="exitCode">The exit code curl gives the same failure.</param>
/// <param name="message">A description of the failure.</param>
internal sealed class OAuth2HttpException(OAuth2HttpFailure failure, CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets why the exchange failed.</summary>
    public OAuth2HttpFailure Failure { get; } = failure;

    /// <summary>Gets the exit code curl gives the same failure.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>Creates the failure for a response that breaks HTTP/1.1's syntax, which curl reports as a weird server reply.</summary>
    /// <param name="failure">Which rule the response breaks.</param>
    /// <param name="message">A description of the failure.</param>
    /// <returns>The failure to throw.</returns>
    public static OAuth2HttpException Malformed(OAuth2HttpFailure failure, string message) =>
        new(failure, CurlExitCode.WeirdServerReply, message);

    /// <summary>Creates the failure for a connection that closed after the status line, before the response ended.</summary>
    /// <returns>The failure to throw.</returns>
    public static OAuth2HttpException ClosedEarly() =>
        new(OAuth2HttpFailure.ClosedEarly, CurlExitCode.PartialFile, "The connection closed before the response ended.");
}
