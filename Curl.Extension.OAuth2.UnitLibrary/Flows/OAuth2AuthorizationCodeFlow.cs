using System.Buffers.Text;
using System.Net;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// The authorization code grant with PKCE (RFC 6749 §4.1, RFC 7636) through a loopback redirect (RFC 8252 §7.3): listens
/// on <c>http://127.0.0.1:&lt;port&gt;/</c>, tells the user to open the authorization URL, accepts the one redirect,
/// checks its <c>state</c> (RFC 6749 §10.12) and exchanges the code with the verifier (§4.1.3, RFC 7636 §4.5).
/// </summary>
/// <remarks>
/// A redirect whose <c>state</c> is missing or not the one sent, or that carries an <c>error</c> (§4.1.2.1), ends the
/// flow with <see cref="CurlExitCode.LoginDenied"/>; one with neither a code nor an error with
/// <see cref="CurlExitCode.WeirdServerReply"/> (ADR-1045).
/// </remarks>
/// <param name="httpClient">Sends the token request.</param>
/// <param name="listener">Listens on the loopback port the redirect comes to.</param>
/// <param name="messages">Where the authorization URL is written for the user: curl's standard error.</param>
/// <param name="randomBytes">Gives the given number of cryptographically random octets, for the verifier and the state.</param>
/// <param name="request">The token endpoint, client and scope.</param>
/// <param name="authorizationEndpoint">The authorization endpoint (§3.1).</param>
/// <param name="redirectPort">The loopback port to listen on, or 0 for any free one.</param>
public sealed class OAuth2AuthorizationCodeFlow(
    OAuth2HttpClient httpClient,
    IConnectionListener listener,
    TextWriter messages,
    Func<int, byte[]> randomBytes,
    OAuth2FlowRequest request,
    string authorizationEndpoint,
    int redirectPort) : IOAuth2TokenFlow
{
    /// <summary>The random octets the state is made from: 128 bits, as RFC 6749 §10.10 asks of a guess.</summary>
    public const int StateOctets = 16;

    private const string AuthorizationServerName = "authorization server";

    /// <inheritdoc/>
    public async Task<OAuth2FlowResult> RunAsync(CancellationToken cancellationToken)
    {
        var listened = await listener.ListenAsync(new ListenTarget(IPAddress.Loopback, redirectPort, redirectPort), cancellationToken);
        if (listened.PendingConnection is not { } pending)
        {
            return OAuth2FlowResult.Failed(listened.ExitCode, listened.ErrorMessage!);
        }

        await using (pending)
        {
            var redirectUri = $"http://127.0.0.1:{((IPEndPoint)pending.LocalEndPoint).Port}/";
            var codeVerifier = OAuth2Pkce.CreateCodeVerifier(randomBytes(OAuth2Pkce.MinimumRandomOctets));
            var state = Base64Url.EncodeToString(randomBytes(StateOctets));
            var authorizationUrl = new OAuth2AuthorizationRequest(
                authorizationEndpoint, request.Client.ClientId, redirectUri, request.Scope, state, OAuth2Pkce.CreateS256CodeChallenge(codeVerifier)).BuildUrl();
            await messages.WriteAsync(DescribeInstructions(authorizationUrl).AsMemory(), cancellationToken);
            await messages.FlushAsync(cancellationToken);
            var (redirect, failure) = await OAuth2RedirectReceiver.ReceiveAsync(pending, cancellationToken);
            if (redirect is null)
            {
                return failure!;
            }

            return CheckRedirect(redirect, state) ?? await OAuth2TokenEndpoint.RequestTokenAsync(
                httpClient,
                request.TokenEndpoint,
                OAuth2TokenRequestWriter.WriteAuthorizationCode(request.Client, redirect.Find("code")!, redirectUri, codeVerifier),
                cancellationToken);
        }
    }

    /// <summary>What the user is told: to open the authorization URL in a browser.</summary>
    /// <param name="authorizationUrl">The authorization request URL (§4.1.1).</param>
    /// <returns>The lines, each ending in a newline.</returns>
    public static string DescribeInstructions(string authorizationUrl) =>
        $"To sign in, open this address in a browser:\n{authorizationUrl}\n";

    private static OAuth2FlowResult? CheckRedirect(OAuth2Redirect redirect, string state)
    {
        if (redirect.Find("state") != state)
        {
            return OAuth2FlowResult.Failed(
                CurlExitCode.LoginDenied, "The redirect's state is not the one curl sent, so it may not answer curl's request; the sign-in was abandoned.");
        }

        if (redirect.Find("error") is { } error)
        {
            return OAuth2FlowResult.Failed(
                CurlExitCode.LoginDenied, OAuth2FlowResult.DescribeRefusal(AuthorizationServerName, error, redirect.Find("error_description")));
        }

        return redirect.Find("code") is null
            ? OAuth2FlowResult.Failed(CurlExitCode.WeirdServerReply, "The redirect carried neither a code nor an error.")
            : null;
    }
}
