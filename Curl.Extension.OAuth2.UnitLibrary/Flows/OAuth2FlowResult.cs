using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// How a flow ended (ADR-1045): with an access token, refused by an OAuth 2.0 error response, or failed with curl's exit
/// code and the message it prints.
/// </summary>
public sealed class OAuth2FlowResult
{
    private OAuth2FlowResult(OAuth2TokenResponse? token, OAuth2ErrorResponse? error, CurlExitCode exitCode, string? message)
    {
        Token = token;
        Error = error;
        ExitCode = exitCode;
        Message = message;
    }

    /// <summary>Gets the access token response (§5.1), or <see langword="null"/> when the flow failed.</summary>
    public OAuth2TokenResponse? Token { get; }

    /// <summary>Gets the error response an endpoint answered with (§5.2, RFC 8628 §3.5), when that is why the flow failed.</summary>
    public OAuth2ErrorResponse? Error { get; }

    /// <summary>Gets curl's exit code: <see cref="CurlExitCode.Ok"/> with a token.</summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>Gets the message for the failure, or <see langword="null"/> with a token.</summary>
    public string? Message { get; }

    /// <summary>Gets whether the flow got a token.</summary>
    public bool Succeeded => Token is not null;

    /// <summary>A flow that got <paramref name="token"/>.</summary>
    /// <param name="token">The access token response.</param>
    /// <returns>The result.</returns>
    public static OAuth2FlowResult Issued(OAuth2TokenResponse token) => new(token, null, CurlExitCode.Ok, null);

    /// <summary>
    /// A flow an endpoint refused with <paramref name="error"/>: <see cref="CurlExitCode.OperationTimedOut"/> for
    /// <c>expired_token</c>, otherwise <see cref="CurlExitCode.LoginDenied"/>.
    /// </summary>
    /// <param name="error">The error response.</param>
    /// <param name="endpointName">The endpoint that refused, such as "token endpoint".</param>
    /// <returns>The result.</returns>
    public static OAuth2FlowResult Refused(OAuth2ErrorResponse error, string endpointName) => new(
        null,
        error,
        error.Code == OAuth2ErrorCode.ExpiredToken ? CurlExitCode.OperationTimedOut : CurlExitCode.LoginDenied,
        DescribeRefusal(endpointName, error.Error, error.Description));

    /// <summary>A flow that failed for a reason other than an error response.</summary>
    /// <param name="exitCode">Curl's exit code for the failure.</param>
    /// <param name="message">What went wrong.</param>
    /// <returns>The result.</returns>
    public static OAuth2FlowResult Failed(CurlExitCode exitCode, string message) => new(null, null, exitCode, message);

    internal static string DescribeRefusal(string endpointName, string error, string? description) =>
        description is null ? $"The {endpointName} refused the request: {error}." : $"The {endpointName} refused the request: {error} ({description}).";
}
