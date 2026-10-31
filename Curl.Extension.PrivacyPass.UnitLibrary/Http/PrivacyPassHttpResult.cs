using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Http;

/// <summary>The outcome of one request to an issuer: the response, or why none was read.</summary>
public sealed class PrivacyPassHttpResult
{
    private PrivacyPassHttpResult(PrivacyPassHttpResponse? response, PrivacyPassHttpFailure? failure, CurlExitCode exitCode, string? message)
    {
        Response = response;
        Failure = failure;
        ExitCode = exitCode;
        Message = message;
    }

    /// <summary>Gets the response, or <see langword="null"/> when the exchange failed.</summary>
    public PrivacyPassHttpResponse? Response { get; }

    /// <summary>Gets why the exchange failed, or <see langword="null"/> when a response was read.</summary>
    public PrivacyPassHttpFailure? Failure { get; }

    /// <summary>Gets the exit code curl gives the failure, or <see cref="CurlExitCode.Ok"/> when a response was read.</summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>Gets a description of the failure, or <see langword="null"/> when a response was read.</summary>
    public string? Message { get; }

    /// <summary>Gets a value indicating whether a response was read.</summary>
    public bool Succeeded => Response is not null;

    /// <summary>Creates the outcome of an exchange that read <paramref name="response"/>.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The outcome.</returns>
    public static PrivacyPassHttpResult Received(PrivacyPassHttpResponse response) => new(response, null, CurlExitCode.Ok, null);

    /// <summary>Creates the outcome of an exchange that failed.</summary>
    /// <param name="failure">Why it failed.</param>
    /// <param name="exitCode">The exit code curl gives the failure.</param>
    /// <param name="message">A description of the failure.</param>
    /// <returns>The outcome.</returns>
    public static PrivacyPassHttpResult Failed(PrivacyPassHttpFailure failure, CurlExitCode exitCode, string message) =>
        new(null, failure, exitCode, message);
}
