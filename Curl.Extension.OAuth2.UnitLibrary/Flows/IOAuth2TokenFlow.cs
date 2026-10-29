namespace Curl.Extension.OAuth2.Flows;

/// <summary>One way of getting an access token from the authorization server: a grant (RFC 6749 §1.3).</summary>
public interface IOAuth2TokenFlow
{
    /// <summary>Runs the grant to its end.</summary>
    /// <param name="cancellationToken">Cancels the flow.</param>
    /// <returns>The access token, or curl's exit code and message for why there is none.</returns>
    Task<OAuth2FlowResult> RunAsync(CancellationToken cancellationToken);
}
