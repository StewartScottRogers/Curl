using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>An exchange with a registry or its token endpoint failed; <see cref="OciHttpClient"/> turns it into a failed <see cref="OciHttpResult"/>.</summary>
/// <param name="failure">Why the exchange failed.</param>
/// <param name="exitCode">The exit code curl gives the same failure.</param>
/// <param name="message">A description of the failure.</param>
internal sealed class OciHttpException(OciHttpFailure failure, CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets why the exchange failed.</summary>
    public OciHttpFailure Failure { get; } = failure;

    /// <summary>Gets the exit code curl gives the same failure.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>Creates the failure for a response that breaks HTTP/1.1's syntax, which curl reports as a weird server reply.</summary>
    /// <param name="failure">Which rule the response breaks.</param>
    /// <param name="message">A description of the failure.</param>
    /// <returns>The failure to throw.</returns>
    public static OciHttpException Malformed(OciHttpFailure failure, string message) =>
        new(failure, CurlExitCode.WeirdServerReply, message);

    /// <summary>Creates the failure for a connection that closed after the status line, before the response ended.</summary>
    /// <returns>The failure to throw.</returns>
    public static OciHttpException ClosedEarly() =>
        new(OciHttpFailure.ClosedEarly, CurlExitCode.PartialFile, "The connection closed before the response ended.");
}
