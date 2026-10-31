using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>Posts a form to an authorization server endpoint and reads its answer: the response it asked for, or why not.</summary>
internal static class OAuth2TokenEndpoint
{
    public const string TokenEndpointName = "token endpoint";

    /// <summary>Posts a token request (§4.4.2, §4.1.3, RFC 8628 §3.4) and reads the token (§5.1) or the error (§5.2).</summary>
    public static async Task<OAuth2FlowResult> RequestTokenAsync(
        OAuth2HttpClient httpClient, Uri tokenEndpoint, OAuth2FormRequest form, CancellationToken cancellationToken)
    {
        var (token, failure) = await PostAsync(
            httpClient, tokenEndpoint, form, OAuth2TokenResponseParser.Parse, TokenEndpointName, cancellationToken);
        return token is null ? failure! : OAuth2FlowResult.Issued(token);
    }

    /// <summary>
    /// Posts <paramref name="form"/> and reads a 200 answer with <paramref name="parse"/>; any other answer that is an
    /// error response is a refusal, and one that is neither is <see cref="CurlExitCode.WeirdServerReply"/>.
    /// </summary>
    public static async Task<(T? Value, OAuth2FlowResult? Failure)> PostAsync<T>(
        OAuth2HttpClient httpClient,
        Uri endpoint,
        OAuth2FormRequest form,
        Func<string, OAuth2ParseResult<T>> parse,
        string endpointName,
        CancellationToken cancellationToken)
        where T : class
    {
        var result = await httpClient.PostFormAsync(endpoint, form, cancellationToken);
        if (result.Response is not { } response)
        {
            return (null, OAuth2FlowResult.Failed(result.ExitCode, result.Message!));
        }

        return ReadAnswer(response, parse, endpointName);
    }

    private static (T? Value, OAuth2FlowResult? Failure) ReadAnswer<T>(
        OAuth2HttpResponse response, Func<string, OAuth2ParseResult<T>> parse, string endpointName)
        where T : class
    {
        var parsed = response.StatusCode == 200 ? parse(response.Body) : null;
        if (parsed?.Value is { } value)
        {
            return (value, null);
        }

        if (OAuth2ErrorResponseParser.Parse(response.Body).Value is { } error)
        {
            return (null, OAuth2FlowResult.Refused(error, endpointName));
        }

        var reason = parsed is null ? $"{response.StatusCode} {response.ReasonPhrase}" : parsed.Failure;
        return (null, OAuth2FlowResult.Failed(CurlExitCode.WeirdServerReply, $"The {endpointName} gave an answer curl cannot use: {reason}"));
    }
}
