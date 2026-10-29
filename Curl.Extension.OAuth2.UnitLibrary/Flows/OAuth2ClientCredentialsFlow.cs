using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>The client credentials grant (RFC 6749 §4.4): one token request with the client's own credentials.</summary>
/// <param name="httpClient">Sends the token request.</param>
/// <param name="request">The token endpoint, client and scope.</param>
public sealed class OAuth2ClientCredentialsFlow(OAuth2HttpClient httpClient, OAuth2FlowRequest request) : IOAuth2TokenFlow
{
    /// <inheritdoc/>
    public Task<OAuth2FlowResult> RunAsync(CancellationToken cancellationToken) => OAuth2TokenEndpoint.RequestTokenAsync(
        httpClient, request.TokenEndpoint, OAuth2TokenRequestWriter.WriteClientCredentials(request.Client, request.Scope), cancellationToken);
}
