using System.Net;
using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Signs every request to the origin that carries <see cref="HttpAuthRequest.AwsSigV4" /> with
/// AWS Signature Version 4, as curl 8.21.0's <c>--aws-sigv4</c> does in place of every other
/// scheme, and hands every other request to <paramref name="otherSchemes" /> (BL-629, ADR-0243).
/// </summary>
/// <remarks>
/// Signing happens here, in an authenticator, rather than in the request options, because the
/// HTTP handler asks its authenticator afresh for every request it sends, each hop of a
/// followed redirect included, and curl signs each hop for its own path and query (measured,
/// BL-629 Notes). The value is the <c>Authorization</c> value followed by the
/// <c>X-Amz-Date</c> and <c>x-amz-content-sha256</c> lines, which curl sends in the
/// <c>Authorization</c> slot (<see cref="IHttpAuthenticator.CreateAuthorization" />). Signing is
/// only ever pre-emptive: a 401's challenge is not answered, so it is the transfer's result.
/// </remarks>
/// <param name="otherSchemes">Answers every request without <see cref="HttpAuthRequest.AwsSigV4" />.</param>
/// <param name="signer">Signs a request.</param>
internal sealed class AwsSigV4HttpAuthenticator(IHttpAuthenticator otherSchemes, AwsSigV4Signer signer) : IHttpAuthenticator
{
    /// <summary>What the value <see cref="AwsSigV4Signer" /> gives for <c>Authorization</c> starts with.</summary>
    private const string AuthorizationLinePrefix = "Authorization: ";

    /// <summary>What precedes the signature in the <c>Authorization</c> value.</summary>
    private const string SignatureMarker = "Signature=";

    /// <inheritdoc />
    /// <exception cref="HttpAuthenticationFailedException">curl refuses to sign the request.</exception>
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.AwsSigV4 is { } inputs
            ? Sign(request, inputs, challenges)
            : otherSchemes.CreateAuthorization(request, challenges);
    }

    /// <inheritdoc />
    /// <exception cref="HttpAuthenticationFailedException">curl refuses to sign the request.</exception>
    public ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.AwsSigV4 is { } inputs
            ? ValueTask.FromResult(Sign(request, inputs, challenges))
            : otherSchemes.CreateAuthorizationAsync(request, challenges, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.AwsSigV4 is null
            ? otherSchemes.ContinueAuthorizationAsync(request, sentAuthorization, sentBeforeAnyChallenge, challenges, cancellationToken)
            : ValueTask.FromResult<string?>(null);
    }

    /// <summary>
    /// Signs <paramref name="request" /> before any challenge, when it has a credential, and
    /// reports the <c>-v</c> lines curl 8.21.0 prints for it: the string to sign, the
    /// signature, and <c>Server auth using AWS_SIGV4 with user '...'</c>, the last even when
    /// an <c>-H</c> <c>Authorization</c> leaves the request unsigned (measured, BL-629 Notes).
    /// </summary>
    /// <returns>The value to send, or <see langword="null" /> to send none.</returns>
    /// <exception cref="HttpAuthenticationFailedException">curl refuses to sign the request.</exception>
    private string? Sign(HttpAuthRequest request, AwsSigV4Inputs inputs, IReadOnlyList<string> challenges)
    {
        if (challenges.Count != 0 || request.Credential is not { } credential)
        {
            return null;
        }

        AwsSigV4SigningResult result = signer.Sign(SigningRequestOf(request, inputs, credential));
        if (result.ErrorMessage is { } message)
        {
            throw new HttpAuthenticationFailedException(result.ExitCode, message);
        }

        string? value = null;
        if (result.HeaderLines.Count != 0)
        {
            string authorization = result.HeaderLines[0];
            request.Events.ReportInfo($"aws_sigv4: String to sign (enclosed in []) - [{result.StringToSign}]");
            request.Events.ReportInfo("aws_sigv4: Signature - " + authorization[(authorization.LastIndexOf(SignatureMarker, StringComparison.Ordinal) + SignatureMarker.Length)..]);
            value = string.Join("\r\n", result.HeaderLines)[AuthorizationLinePrefix.Length..];
        }

        request.Events.ReportInfo($"Server auth using AWS_SIGV4 with user '{credential.UserName}'");
        return value;
    }

    /// <summary>
    /// Maps <paramref name="request" /> to what <see cref="AwsSigV4Signer" /> signs: its
    /// method, the URL's host name, path and query, and <paramref name="inputs" />.
    /// </summary>
    private static AwsSigV4Request SigningRequestOf(HttpAuthRequest request, AwsSigV4Inputs inputs, NetworkCredential credential) =>
        new()
        {
            SigV4Parameter = inputs.Parameter,
            UserName = credential.UserName,
            Password = credential.Password,
            Method = request.Method,
            HostName = request.Url.IdnHost,
            HostHeaderValue = inputs.HostHeaderValue,
            Path = request.Url.AbsolutePath,
            Query = request.Url.Query,
            CustomHeaders = inputs.CustomHeaders,
            IsGetOrHead = inputs.IsGetOrHead,
            PostFields = inputs.PostFields?.ToArray(),
            UploadFileSize = inputs.UploadSize,
            PathAsIs = inputs.PathAsIs,
        };
}
