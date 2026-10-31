using System.Net;
using Curl.Extension.Protocol.OciRegistry.Auth;
using Curl.Extension.Protocol.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.OciRegistry.Client;

/// <summary>
/// What a pull and a push share: sending one request and turning a failed exchange into an
/// <see cref="OciTransferException"/>, and answering a 401's <c>Bearer</c> challenge with the token its realm gives
/// (Docker/Distribution token authentication specification, "How to authenticate").
/// </summary>
/// <param name="client">Sends each request.</param>
/// <param name="credentials"><c>-u</c>'s user and password, sent to the token endpoint, or <see langword="null"/> for an anonymous token.</param>
internal sealed class OciRegistrySession(OciHttpClient client, NetworkCredential? credentials)
{
    /// <summary>Sends <paramref name="request"/> and reads its response, whatever its status.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response.</returns>
    /// <exception cref="OciTransferException">No response was read.</exception>
    public async Task<OciHttpResponse> ReceiveAsync(OciHttpRequest request, CancellationToken cancellationToken)
    {
        var result = await client.SendAsync(request, cancellationToken);
        return result.Response ?? throw new OciTransferException(result.ExitCode, result.Message!);
    }

    /// <summary>Asks the realm of <paramref name="challengeResponse"/>'s challenge for a token.</summary>
    /// <param name="challengeResponse">The registry's 401.</param>
    /// <param name="scope">The scope to ask for in place of the challenge's, or <see langword="null"/> to ask for the challenge's own.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The <c>Authorization</c> value, <c>Bearer</c> and the token.</returns>
    /// <exception cref="OciTransferException">
    /// The 401 has no <c>Bearer</c> challenge with a usable realm (the registry's own error), or the token endpoint gave no token.
    /// </exception>
    public async Task<string> RequestAuthorizationAsync(OciHttpResponse challengeResponse, string? scope, CancellationToken cancellationToken)
    {
        var response = await ReceiveAsync(TokenRequest(challengeResponse, scope), cancellationToken);
        var token = response.StatusCode is >= 200 and < 300 ? OciTokenResponseParser.ParseToken(response.Body) : null;
        return token is null
            ? throw new OciTransferException(CurlExitCode.LoginDenied, $"The token endpoint answered {response.StatusCode} without a token.")
            : "Bearer " + token;
    }

    // A 401 without a Bearer challenge that has a usable realm cannot be answered, so it fails as the registry's own error.
    private OciHttpRequest TokenRequest(OciHttpResponse challengeResponse, string? scope)
    {
        var challenge = OciBearerChallengeParser.Parse(challengeResponse.FindHeader("WWW-Authenticate"));
        return (challenge is null ? null : OciTokenRequestBuilder.Build(challenge with { Scope = scope ?? challenge.Scope }, UserAndPassword()))
            ?? throw OciRegistryError.From(challengeResponse);
    }

    private string? UserAndPassword() => credentials is null ? null : credentials.UserName + ":" + credentials.Password;
}
