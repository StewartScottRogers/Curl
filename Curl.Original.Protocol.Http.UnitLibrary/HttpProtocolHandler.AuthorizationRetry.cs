using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that answers an authentication challenge with a new request.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Decides whether a response is answered with one more request, and with what
    /// <c>Authorization</c> value: only a 401, only when its body can be sent again, and only
    /// when the authenticator answers the response's <c>WWW-Authenticate</c> challenges -
    /// through <see cref="IHttpAuthenticator.ContinueAuthorizationAsync" /> when the request
    /// that drew it already sent one, which only a handshake of more than one leg (NTLM,
    /// Negotiate) answers, so a credential sent up front and refused ends the transfer, as in
    /// curl 8.21.0 (ADR-0181, ADR-0227).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private ValueTask<string?> RetryAuthorizationAsync(HttpRequestPlan plan, HttpAuthRequest request, HttpResponseHead head, CancellationToken cancellationToken) =>
        RepeatsKeptDigestAnswer(plan, head)
            ? ValueTask.FromResult<string?>(Authenticator.RepeatAuthorization(request, plan.Authorization!))
            : AnswerChallengesAsync(
                request,
                plan.Authorization,
                plan.AuthorizationAnswersChallenge,
                MayRetry(plan, head, 401) ? ValuesOf(head, "WWW-Authenticate") : [],
                cancellationToken);

    /// <summary>
    /// Decides whether a 401 to a first request that answered the handle's kept Digest challenges
    /// (<see cref="HttpRequestPlan.AuthorizationAnswersKeptDigestChallenges" />) is answered by sending
    /// that answer again, counted on, when the previous perform ended with Digest still available
    /// (<see cref="HttpRequestOptions.DigestStaysAvailable" />) and the 401 offers Digest: libcurl 8.22.0
    /// keeps <c>authhost.avail</c> on the easy handle from the 401 that ended a perform with
    /// <c>authproblem</c> set, so <c>Curl_http_input_auth</c> writes <c>Ignoring duplicate digest auth
    /// header.</c> for this 401's challenge, keeps the old nonce, and retries once before the next 401
    /// ends the transfer (upstream test2027 and test2030, BL-2745, BL-2823, ADR-0700, ADR-0709). Otherwise
    /// <c>pickoneauth</c> cleared <c>avail</c>, so the 401 ends the transfer (upstream test2024).
    /// </summary>
    private static bool RepeatsKeptDigestAnswer(HttpRequestPlan plan, HttpResponseHead head) =>
        plan.AuthorizationAnswersKeptDigestChallenges
            && plan.Options.DigestStaysAvailable
            && MayRetry(plan, head, 401)
            && ValuesOf(head, "WWW-Authenticate").Any(challenge => SchemeOfAuthorization(challenge) == HttpAuthSchemes.Digest);

    /// <summary>
    /// Makes the request <see cref="RetryAuthorizationAsync" /> answers a 401 with, or gives
    /// <see langword="null" /> when it answers none. What the authenticator reports while it
    /// answers goes where curl 8.21.0 writes it (ADR-0232): before the retry when the request
    /// that drew the 401 was not sent with Negotiate picked, as curl steps that context on the
    /// way out, and else at once, just before the Negotiate challenge header (ADR-0231), as it
    /// does when there is no retry. A refusal to answer when Negotiate was not picked fails the
    /// retry instead, once its lines are written, as curl fails making NTLM's Type 3 message on
    /// the way out (measured, BL-848 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />) while Negotiate is picked.</exception>
    private async ValueTask<HttpRequestPlan?> RetryWithAuthorizationAsync(HttpRequestPlan plan, HttpResponseHead head, CancellationToken cancellationToken)
    {
        if (ClosesWhileNegotiatingNtlm(plan, head))
        {
            return null;
        }

        HttpInfoLineRecorder retryLines = new();
        bool picksNegotiate = HttpNegotiateInfoLines.PicksNegotiate(plan.AuthRequest, plan.Authorization, plan.AuthorizationAnswersChallenge);
        HttpAuthRequest request = picksNegotiate ? plan.AuthRequest : plan.AuthRequest with { Events = retryLines };
        if (plan.Options.RequestTarget is null)
        {
            request = request with { RequestTarget = UrlPathEscapes.WithUpperCaseHexDigits(request.RequestTarget) };
        }

        string? authorization;
        try
        {
            authorization = await RetryAuthorizationAsync(plan, request, head, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException failure) when (!picksNegotiate)
        {
            // curl keeps the response whose challenge it could not answer: its code and header count (BL-5118).
            failure.StatusLineBeforeFailure = head.StatusLine;
            failure.HeadersStoredBeforeFailure = head.Headers.Count + 1;
            failure.AnsweredHeadersBeforeFailure = [.. head.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value))];
            return plan.WithAuthorizationFailure(failure, retryLines.Lines, RepeatProxyAuthorization(plan));
        }

        if (authorization is null)
        {
            ReportInfoLines(plan.Context.Events, retryLines.Lines);
            return null;
        }

        HttpRequestPlan retry = plan.WithAuthorization(authorization, retryLines.Lines, RepeatProxyAuthorization(plan)).HoldingDigestNonceAfter(head);
        retry.RereadsUrl = true;
        return retry;
    }

    /// <summary>
    /// Decides whether <paramref name="challenges" /> offer Digest, whose nonce curl 8.22.0 stores
    /// even when Digest is not allowed (BL-2820).
    /// </summary>
    private static bool OffersDigest(IEnumerable<string> challenges) =>
        challenges.Any(challenge => SchemeOfAuthorization(challenge) == HttpAuthSchemes.Digest);

    /// <summary>
    /// Decides whether a transfer ends with Digest still marked available
    /// (<see cref="TransferReport.DigestStaysAvailable" />), a port of libcurl 8.22.0's
    /// <c>Curl_http_input_auth</c> and <c>Curl_http_auth_act</c> (ADR-0709): the final 401's
    /// Digest challenge adds Digest to <c>authhost.avail</c>, and <c>pickoneauth</c>, which clears
    /// it, is skipped when that 401 set <c>authproblem</c> (<see cref="SetsAuthProblem" />).
    /// </summary>
    private static bool DigestStaysAvailableAfter(bool heldDigestNonce, bool sentNtlmAuthorization, HttpResponseHead head)
    {
        string[] challenges = ValuesOf(head, "WWW-Authenticate");
        return head.StatusLine.StatusCode == 401
            && OffersDigest(challenges)
            && SetsAuthProblem(heldDigestNonce, sentNtlmAuthorization, challenges);
    }

    /// <summary>
    /// Decides whether a 401's <paramref name="challenges" /> set libcurl 8.22.0's
    /// <c>data->state.authproblem</c> ("Authentication problem. Ignoring this."): a Digest
    /// challenge without <c>stale=true</c> after the transfer already held a Digest nonce
    /// (<paramref name="heldDigestNonce" />), which <c>Curl_input_digest</c> fails (upstream test2027,
    /// BL-2820), or a bare <c>NTLM</c> challenge to a request sent with an NTLM answer
    /// (<paramref name="sentNtlmAuthorization" />), which <c>Curl_input_ntlm</c> fails as a rejected
    /// handshake (upstream test2030, BL-2823).
    /// </summary>
    private static bool SetsAuthProblem(bool heldDigestNonce, bool sentNtlmAuthorization, string[] challenges) =>
        (heldDigestNonce && !HttpDigestStaleChallenge.IsOfferedIn(challenges))
            || (sentNtlmAuthorization && challenges.Any(challenge => challenge.Trim().Equals("NTLM", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Makes the <c>Authorization</c> value <paramref name="plan" /> sent as
    /// <see cref="IHttpAuthenticator.RepeatAuthorization" /> sends it again, so a Digest answer
    /// counts its nonce on as in curl 8.21.0 (BL-869); <see langword="null" /> when it sent none.
    /// </summary>
    private string? RepeatAuthorization(HttpRequestPlan plan) =>
        plan.Authorization is { } sent ? Authenticator.RepeatAuthorization(plan.AuthRequest, sent) : null;

    /// <summary>
    /// Makes the <c>Authorization</c> value a retry that answers a 407 keeps:
    /// <see cref="RepeatAuthorization" />'s, but none after an NTLM Type 3 message, which curl
    /// 8.22.0 sends once. The resend that answers the proxy carries no <c>Authorization</c>, so a
    /// server that did not accept the handshake is asked to start another, until the redirect
    /// limit ends the transfer (measured, BL-5892).
    /// </summary>
    private string? KeptAuthorizationForProxyRetry(HttpRequestPlan plan) =>
        plan.Authorization is { } sent && IsNtlmType3(sent) ? null : RepeatAuthorization(plan);

    /// <summary>
    /// Makes the <c>Proxy-Authorization</c> value a retry that answers a 401 keeps, as
    /// <see cref="RepeatAuthorization" /> makes the <c>Authorization</c> value a 407's retry keeps;
    /// none after an NTLM Type 3 message, whose handshake authenticated the connection, as
    /// curl 8.21.0 sends only the origin's answer after it (upstream test169, BL-2030).
    /// </summary>
    private string? RepeatProxyAuthorization(HttpRequestPlan plan) =>
        plan.ProxyAuthorization is { } sent && IsNtlmType3(sent)
            ? null
            : plan.ProxyAuthRequest is { } request && plan.ProxyAuthorization is { } kept
                ? Authenticator.RepeatAuthorization(request, kept)
                : plan.ProxyAuthorization;

    /// <summary>
    /// Decides whether a response is answered with one more request, and with what
    /// <c>Proxy-Authorization</c> value: only a 407 from a forward proxy, on the same terms as
    /// <see cref="RetryAuthorizationAsync" /> sets for a 401, with the response's
    /// <c>Proxy-Authenticate</c> challenges and the proxy's request (ADR-0239).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private ValueTask<string?> RetryProxyAuthorizationAsync(HttpRequestPlan plan, HttpResponseHead head, CancellationToken cancellationToken) =>
        plan.ProxyAuthRequest is { } proxyRequest && MayRetry(plan, head, 407)
            ? AnswerChallengesAsync(proxyRequest, plan.ProxyAuthorization, plan.ProxyAuthorizationAnswersChallenge, ValuesOf(head, "Proxy-Authenticate"), cancellationToken)
            : ValueTask.FromResult<string?>(null);

    /// <summary>
    /// Asks the authenticator to answer <paramref name="challenges" /> for
    /// <paramref name="request" />: nothing when there are none; through
    /// <see cref="IHttpAuthenticator.ContinueAuthorizationAsync" /> when the request that drew
    /// them already sent <paramref name="sent" />, even an empty one, an empty answer to which
    /// sends nothing more, so the request is never sent again without a header twice
    /// (ADR-0232); and else afresh - as is a Digest answer whose nonce the challenges mark
    /// <c>stale=true</c>, which curl 8.21.0 answers again with the new nonce, without limit
    /// (measured, BL-1148 Notes).
    /// </summary>
    /// <exception cref="HttpTransferException">The authenticator fails the transfer (<see cref="HttpAuthenticationFailedException" />).</exception>
    private async ValueTask<string?> AnswerChallengesAsync(HttpAuthRequest request, string? sent, bool sentAnswersChallenge, string[] challenges, CancellationToken cancellationToken)
    {
        if (challenges.Length == 0)
        {
            return null;
        }

        try
        {
            return sent is not null && !RenewsStaleDigest(sent, challenges)
                ? NullIfEmpty(await Authenticator.ContinueAuthorizationAsync(request, sent, !sentAnswersChallenge, challenges, cancellationToken).ConfigureAwait(false))
                : await Authenticator.CreateAuthorizationAsync(request, challenges, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            throw new HttpTransferException(failure.ExitCode, failure.Message);
        }
    }

    /// <summary>
    /// Decides whether <paramref name="challenges" /> mark the nonce of the Digest answer
    /// <paramref name="sent" /> stale, so it is answered afresh with the new nonce.
    /// </summary>
    private static bool RenewsStaleDigest(string sent, string[] challenges) =>
        sent.StartsWith("Digest ", StringComparison.Ordinal) && HttpDigestStaleChallenge.IsOfferedIn(challenges);

    /// <summary>Gives <paramref name="value" />, or <see langword="null" /> when it is empty.</summary>
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
