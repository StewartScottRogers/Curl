using Curl.HttpMessageSignatures;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Signs every request to the origin that carries <see cref="HttpAuthRequest.HttpSignature" />
/// with an RFC 9421 HTTP message signature, as curl 8.22.0's <c>--httpsig-*</c> options do in
/// place of every other scheme, and hands every other request to <paramref name="otherSchemes" />
/// (BL-2182, ADR-0485).
/// </summary>
/// <remarks>
/// Signing happens here, as for <c>--aws-sigv4</c> (<see cref="AwsSigV4HttpAuthenticator" />),
/// because the HTTP handler asks its authenticator afresh for every request it sends, so each
/// followed hop is signed for its own path and query (upstream test 5024). The value is the
/// <c>Signature-Input</c> and <c>Signature</c> lines, which the handler writes in the
/// <c>Authorization</c> slot. Signing is only ever pre-emptive: a 401's challenge is not answered.
/// </remarks>
/// <param name="otherSchemes">Answers every request without <see cref="HttpAuthRequest.HttpSignature" />.</param>
/// <param name="clock">Gives the <c>created</c> time.</param>
internal sealed class HttpSignatureHttpAuthenticator(IHttpAuthenticator otherSchemes, TimeProvider clock) : IHttpAuthenticator
{
    /// <inheritdoc />
    /// <exception cref="HttpAuthenticationFailedException">curl refuses to sign the request, exit 43.</exception>
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.HttpSignature is { } inputs
            ? Sign(request, inputs, challenges)
            : otherSchemes.CreateAuthorization(request, challenges);
    }

    /// <inheritdoc />
    /// <exception cref="HttpAuthenticationFailedException">curl refuses to sign the request, exit 43.</exception>
    public ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.HttpSignature is { } inputs
            ? ValueTask.FromResult(Sign(request, inputs, challenges))
            : otherSchemes.CreateAuthorizationAsync(request, challenges, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.HttpSignature is null
            ? otherSchemes.ContinueAuthorizationAsync(request, sentAuthorization, sentBeforeAnyChallenge, challenges, cancellationToken)
            : ValueTask.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.HttpSignature is null
            ? otherSchemes.RepeatAuthorization(request, sentAuthorization)
            : sentAuthorization;
    }

    /// <inheritdoc />
    public void EndAuthorization(string sentAuthorization) =>
        otherSchemes.EndAuthorization(sentAuthorization);

    /// <summary>
    /// Signs <paramref name="request" /> before any challenge and reports the <c>-v</c> lines
    /// curl 8.22.0 prints for it - the parameters, the signature base, both header lines - then
    /// <c>Server auth using HTTPSIG with user '...'</c>, the last also when an <c>-H</c>
    /// <c>Signature</c> or <c>Signature-Input</c> leaves the request unsigned (upstream test 5017).
    /// </summary>
    /// <returns>The <c>Signature-Input</c> and <c>Signature</c> lines, or <see langword="null" /> to send none.</returns>
    /// <exception cref="HttpAuthenticationFailedException">curl refuses to sign the request, exit 43.</exception>
    private string? Sign(HttpAuthRequest request, HttpSignatureInputs inputs, IReadOnlyList<string> challenges)
    {
        if (challenges.Count != 0)
        {
            return null;
        }

        HttpSignatureHeaders? headers = CreateHeaders(request, inputs);
        string? value = null;
        if (headers is not null)
        {
            request.Events.ReportInfo("httpsig: Signature-Input params: " + headers.SignatureParameters);
            request.Events.ReportInfo($"httpsig: Signature base: [{headers.SignatureBase}]");
            request.Events.ReportInfo("httpsig: Signature-Input: " + headers.SignatureInput);
            request.Events.ReportInfo("httpsig: Signature: " + headers.Signature);
            value = $"Signature-Input: {headers.SignatureInput}\r\nSignature: {headers.Signature}";
        }

        request.Events.ReportInfo($"Server auth using HTTPSIG with user '{request.Credential?.UserName}'");
        return value;
    }

    /// <summary>
    /// Signs through <see cref="CurlHttpSignature.CreateHeaders" />, the request's method, its
    /// URL's path and query, <paramref name="inputs" /> and the clock's time in Unix seconds.
    /// </summary>
    /// <exception cref="HttpAuthenticationFailedException">The library refuses, exit 43 with its message.</exception>
    private HttpSignatureHeaders? CreateHeaders(HttpAuthRequest request, HttpSignatureInputs inputs)
    {
        HttpSignatureSettings settings = inputs.Settings;
        HttpSignatureAlgorithm algorithm = settings.Algorithm == HttpSignatureAlgorithmNames.HmacSha256 ? HttpSignatureAlgorithm.HmacSha256 : HttpSignatureAlgorithm.Ed25519;
        HttpSignatureRequest signed = new(request.Method, inputs.Authority, request.Url.AbsolutePath, request.Url.Query, inputs.CustomHeaders);
        try
        {
            return CurlHttpSignature.CreateHeaders(algorithm, settings.Key, settings.KeyId, settings.Components, signed, clock.GetUtcNow().ToUnixTimeSeconds());
        }
        catch (HttpSignatureException exception)
        {
            throw new HttpAuthenticationFailedException(CurlExitCode.BadFunctionArgument, exception.Message);
        }
    }
}
