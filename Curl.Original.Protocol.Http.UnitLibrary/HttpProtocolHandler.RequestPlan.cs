using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that defines HttpRequestPlan, what one request is to send.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// One request as the handler sends it: the transfer, its options and framing, what the
    /// authenticator is asked about, and the <c>Authorization</c> value it is sent with.
    /// </summary>
    /// <param name="Context">The transfer.</param>
    /// <param name="Options">The transfer's HTTP options, defaults when it has none.</param>
    /// <param name="Framing">The request's method and body framing.</param>
    /// <param name="AuthRequest">The request as the authenticator is asked about it.</param>
    /// <param name="Authorization">
    /// The <c>Authorization</c> value to send, or <see langword="null" /> to send none.
    /// </param>
    private sealed class HttpRequestPlan(
        ITransferContext Context,
        HttpRequestOptions Options,
        HttpRequestFraming Framing,
        HttpAuthRequest AuthRequest,
        string? Authorization)
    {
        /// <summary>Gets the transfer.</summary>
        public ITransferContext Context { get; } = Context;

        /// <summary>Gets the transfer's HTTP options, defaults when it has none.</summary>
        public HttpRequestOptions Options { get; private set; } = Options;

        /// <summary>Gets the request's method and body framing.</summary>
        public HttpRequestFraming Framing { get; } = Framing;

        /// <summary>
        /// Gets the request as the authenticator is asked about it, carrying the connection's
        /// TLS server certificate once <see cref="TakeServerCertificateFrom" /> has run.
        /// </summary>
        public HttpAuthRequest AuthRequest { get; private set; } = AuthRequest;

        /// <summary>
        /// Gets the <c>Authorization</c> value to send, or <see langword="null" /> to send none.
        /// </summary>
        public string? Authorization { get; } = Authorization;

        /// <summary>
        /// Gets the time limits the transfer runs under, whose token cancels every read and
        /// write of the exchange.
        /// </summary>
        public required HttpTransferDeadline Deadline { get; init; }

        /// <summary>
        /// Gets the moment the transfer began, shared by every request it sends: the origin
        /// of every <c>%{time_*}</c> (<see cref="TransferTimings.Started" />).
        /// </summary>
        public required long Started { get; init; }

        /// <summary>
        /// Gets where the transfer's progress is reported, shared by every request it sends.
        /// </summary>
        public required HttpTransferProgress Progress { get; init; }

        /// <summary>
        /// Gets the proxy the request is forwarded through in absolute form, or
        /// <see langword="null" /> when it goes to the origin, directly or through a tunnel.
        /// </summary>
        public ProxyEndpoint? ForwardProxy { get; init; }

        /// <summary>
        /// Gets the request as the authenticator is asked about it for <see cref="ForwardProxy" />,
        /// or <see langword="null" /> when there is none.
        /// </summary>
        public HttpAuthRequest? ProxyAuthRequest { get; init; }

        /// <summary>
        /// Gets the <c>Proxy-Authorization</c> value to send, or <see langword="null" /> to
        /// send none.
        /// </summary>
        public string? ProxyAuthorization { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="ProxyAuthorization" /> answers a 407's
        /// challenge; <see langword="false" /> for the value made before any challenge.
        /// </summary>
        public bool ProxyAuthorizationAnswersChallenge { get; init; }

        /// <summary>
        /// Gets the <c>-v</c> lines the authenticator reported while it made the
        /// <see cref="ProxyAuthorization" /> value before any challenge, written just before
        /// <c>Proxy auth using ...</c> each time that value is sent, as curl 8.21.0 writes a
        /// <c>--proxy-negotiate</c> context's failure there (measured, BL-604 Notes); empty for
        /// a value that answers a 407.
        /// </summary>
        public IReadOnlyList<string> ProxyAuthorizationInfoLines { get; init; } = [];

        /// <summary>
        /// Gets a value indicating whether the request is being sent again on a fresh connection
        /// because a pooled one died before its response, which happens at most once.
        /// </summary>
        public bool SentOnFreshConnection { get; init; }

        /// <summary>
        /// Gets how many times the request has been sent again on a new connection because the
        /// server refused its HTTP/3 stream (ADR-0187); curl gives up after
        /// <see cref="MaximumStreamRefusedRetries" />.
        /// </summary>
        public int StreamRefusedRetries { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the request is sent over HTTP/1.1 because the server
        /// reset its HTTP/2 stream with <c>HTTP_1_1_REQUIRED</c> (CGR-032): its TLS handshake
        /// offers only <c>http/1.1</c> through ALPN and it is never downgraded again.
        /// </summary>
        public bool DowngradedToHttp11 { get; private set; }

        /// <summary>
        /// Gets the message this resend's <c>-T</c> upload failed to rewind with - <c>seek callback
        /// returned error 2</c> for one that cannot seek back to where the refused request began
        /// reading it, as stdin cannot, or an <see cref="IUploadRewinder" />'s own - so the resend fails
        /// with exit 65 before it connects, as curl 8.21.0's <c>cr_in_rewind</c> fails it (ADR-0279);
        /// <see langword="null" /> when the upload was rewound or needs no rewind.
        /// </summary>
        public string? UploadRewindFailure { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether a connection was opened for this request, so a
        /// transfer that failed before one opened keeps its own message (BL-1684).
        /// </summary>
        public bool OpenedConnection { get; set; }

        /// <summary>
        /// Gets how many redirects the transfer has followed before this request: the chain's
        /// count (<see cref="HttpRequestOptions.RedirectsFollowed" />) and one for each resend
        /// after a 417 or in answer to a 401's or 407's challenge, as curl 8.21.0 and 8.22.0 count
        /// them (BL-396 and BL-3256 Notes).
        /// </summary>
        public int RedirectsFollowed { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Authorization" /> answers a challenge, as
        /// every value a retry sends does; <see langword="false" /> for the transfer's first
        /// request, whose value was made before any challenge.
        /// </summary>
        public bool AuthorizationAnswersChallenge { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Authorization" /> is the transfer's first value,
        /// made from the challenges the handle's previous transfer kept
        /// (<see cref="HttpRequestOptions.KeptDigestChallenges" />); <see langword="false" /> for every
        /// value a retry sends (<see cref="RepeatsKeptDigestAnswer" />, BL-2745).
        /// </summary>
        public bool AuthorizationAnswersKeptDigestChallenges { get; init; }

        /// <summary>
        /// Gets a value indicating whether the transfer holds a Digest nonce when this request is
        /// sent: one the handle's previous transfer kept (<see cref="HttpRequestOptions.KeptDigestChallenges" />)
        /// or one an earlier 401 of this transfer offered, which curl 8.22.0 stores even when Digest is
        /// not allowed (<see cref="HoldingDigestNonceAfter" />, BL-2820).
        /// </summary>
        public bool HoldsDigestNonce { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request is one curl builds by re-reading the
        /// URL after a 401, whose target has the query's escapes in upper case
        /// (<see cref="HttpUrlText.OriginTarget(CurlUrl, System.Text.Encoding, bool)" />, BL-3856).
        /// </summary>
        public bool RereadsUrl { get; set; }

        /// <summary>
        /// Marks this retry as holding a Digest nonce (<see cref="HoldsDigestNonce" />) when
        /// <paramref name="head" />, the 401 it answers, offers Digest.
        /// </summary>
        /// <param name="head">The 401 the retry answers.</param>
        /// <returns>This plan.</returns>
        public HttpRequestPlan HoldingDigestNonceAfter(HttpResponseHead head)
        {
            HoldsDigestNonce |= OffersDigest(ValuesOf(head, "WWW-Authenticate"));
            return this;
        }

        /// <summary>
        /// Gets every scheme a 401's <c>WWW-Authenticate</c> challenges offered during the
        /// transfer, up to and including this request's response (<see cref="RecordSchemesOffered" />).
        /// </summary>
        public HttpAuthSchemes AuthSchemesOffered { get; private set; }

        /// <summary>
        /// Gets every scheme a 407's <c>Proxy-Authenticate</c> challenges offered during the
        /// transfer, up to and including this request's response (<see cref="RecordSchemesOffered" />).
        /// </summary>
        public HttpAuthSchemes ProxyAuthSchemesOffered { get; private set; }

        /// <summary>
        /// Adds the schemes <paramref name="head" /> offers to <see cref="AuthSchemesOffered" /> and
        /// <see cref="ProxyAuthSchemesOffered" />, which every retry made from this plan carries, as
        /// libcurl 8.22.0's <c>Curl_http_input_auth</c> ORs each 401 and 407 challenge into
        /// <c>httpauthavail</c> and <c>proxyauthavail</c> (BL-3256).
        /// </summary>
        /// <param name="head">The response the request drew.</param>
        public void RecordSchemesOffered(HttpResponseHead head)
        {
            AuthSchemesOffered |= SchemesChallengedBy(head, 401, "WWW-Authenticate");
            ProxyAuthSchemesOffered |= SchemesChallengedBy(head, 407, "Proxy-Authenticate");
        }

        /// <summary>
        /// Gets the <c>-v</c> lines the authenticator reported while it made
        /// <see cref="Authorization" />, written just before the request each time it is sent:
        /// those of the value made before any challenge (BL-843), or of a value answering a
        /// challenge to a request not sent with Negotiate picked (ADR-0232); empty for a value
        /// whose lines were written with the response's head (ADR-0231).
        /// </summary>
        public IReadOnlyList<string> AuthorizationInfoLines { get; init; } = [];

        /// <summary>
        /// Gets a value indicating whether the request sends no <c>Authorization</c> because the
        /// origin's NTLM Type 3 went out in an earlier request
        /// (<see cref="SendsNothingAfterNtlmType3" />).
        /// </summary>
        public bool NtlmType3WentOut { get; init; }

        /// <summary>
        /// Gets a value indicating whether the request sends no <c>Proxy-Authorization</c> because
        /// the proxy's NTLM Type 3 went out in an earlier request
        /// (<see cref="SendsNothingAfterNtlmType3" />).
        /// </summary>
        public bool ProxyNtlmType3WentOut { get; init; }

        /// <summary>
        /// Gets why the authenticator refused to make the value the transfer's first request is
        /// sent with, which fails the exchange once connected (<see cref="CreateFirstAuthorizationAsync" />);
        /// <see langword="null" /> when it did not refuse.
        /// </summary>
        public HttpTransferException? AuthorizationFailure { get; init; }

        /// <summary>
        /// Makes the same request sent with <paramref name="authorization" /> instead, in answer
        /// to a challenge; an empty value sends no header (ADR-0232).
        /// </summary>
        /// <param name="authorization">The <c>Authorization</c> value the retry is sent with.</param>
        /// <param name="infoLines">The lines the authenticator reported while it made the value that belong just before the retry.</param>
        /// <param name="keptProxyAuthorization">The <c>Proxy-Authorization</c> value the retry keeps, as sent again (BL-869).</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithAuthorization(string authorization, IReadOnlyList<string> infoLines, string? keptProxyAuthorization) =>
            WithAnswer(authorization, SentOnFreshConnection, RedirectsFollowed + 1, authorizationAnswersChallenge: true, keptProxyAuthorization, ProxyAuthorizationAnswersChallenge, infoLines);

        /// <summary>
        /// Gets the framing the probe (<see cref="HttpRequestFraming.AsAuthProbe" />) held back, sent
        /// once a challenge is answered or the probe drew a 2xx; <see langword="null" /> when this
        /// request is no probe.
        /// </summary>
        public HttpRequestFraming? ProbedFraming { get; init; }

        /// <summary>
        /// Makes the same request with the body the probe held back and no credentials, which
        /// curl 8.21.0 sends when the probe drew a 2xx instead of a challenge (upstream test175):
        /// an NTLM Type 1 value the probe carried is not sent again (upstream test176, BL-2029).
        /// </summary>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan WithProbedBody() =>
            With(ProbedFraming!, WithoutNtlmType1(Authorization), SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge, WithoutNtlmType1(ProxyAuthorization), ProxyAuthorizationAnswersChallenge, endsProbe: true);

        /// <summary>
        /// Makes the same request without the NTLM Type 1 <c>Authorization</c> value it carries,
        /// with the body a probe held back for it, for a connection NTLM already authenticated
        /// (BL-3003); this plan itself when its value is no Type 1.
        /// </summary>
        /// <returns>The request's plan on the authenticated connection.</returns>
        public HttpRequestPlan WithoutNtlmType1Probe() =>
            IsNtlmType1(Authorization)
                ? With(ProbedFraming ?? Framing, authorization: null, SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge, ProxyAuthorization, ProxyAuthorizationAnswersChallenge, endsProbe: true)
                : this;

        private static string? WithoutNtlmType1(string? value) => IsNtlmType1(value) ? null : value;

        /// <summary>
        /// Makes the retry that answers a challenge the authenticator refused to answer: it
        /// writes <paramref name="infoLines" /> and then fails with <paramref name="failure" />
        /// before it is sent (<see cref="AuthorizationFailure" />, BL-848).
        /// </summary>
        /// <param name="failure">The refusal the retry fails with.</param>
        /// <param name="infoLines">The lines the authenticator reported while it refused, written just before the failure.</param>
        /// <param name="keptProxyAuthorization">The <c>Proxy-Authorization</c> value the retry keeps.</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithAuthorizationFailure(HttpTransferException failure, IReadOnlyList<string> infoLines, string? keptProxyAuthorization) =>
            With(Framing, authorization: null, SentOnFreshConnection, RedirectsFollowed, authorizationAnswersChallenge: true, keptProxyAuthorization, ProxyAuthorizationAnswersChallenge, infoLines, failure);

        /// <summary>
        /// Makes the same request sent with <paramref name="proxyAuthorization" /> instead, in
        /// answer to a 407's challenge, keeping its <c>Authorization</c> value.
        /// </summary>
        /// <param name="proxyAuthorization">The <c>Proxy-Authorization</c> value the retry is sent with.</param>
        /// <param name="keptAuthorization">The <c>Authorization</c> value the retry keeps, as sent again (BL-869).</param>
        /// <returns>The retry's plan.</returns>
        public HttpRequestPlan WithProxyAuthorization(string proxyAuthorization, string? keptAuthorization) =>
            WithAnswer(keptAuthorization, SentOnFreshConnection, RedirectsFollowed + 1, AuthorizationAnswersChallenge, proxyAuthorization, proxyAuthorizationAnswersChallenge: true);

        /// <summary>
        /// Makes the same request without curl's own <c>Expect</c> and without the wait for
        /// <c>100 Continue</c> (<see cref="HttpRequestFraming.WithoutExpect" />), sending
        /// <paramref name="body" />.
        /// </summary>
        /// <param name="body">The body to resend.</param>
        /// <param name="keepsCustomWait">
        /// <see langword="true" /> to keep an <c>-H</c> <c>Expect: 100-continue</c> wait.
        /// </param>
        /// <returns>The resent request's plan, one more redirect followed.</returns>
        public HttpRequestPlan WithoutExpect(HttpRequestBody body, bool keepsCustomWait) =>
            With(Framing.WithoutExpect(body, keepsCustomWait), Authorization, SentOnFreshConnection, RedirectsFollowed + 1, AuthorizationAnswersChallenge);

        /// <summary>
        /// Makes the same request framed for an HTTP/2 or HTTP/3 stream (<see cref="HttpRequestFraming.ForHttp2OrHttp3" />).
        /// </summary>
        /// <returns>The HTTP/2 or HTTP/3 request's plan.</returns>
        public HttpRequestPlan ForHttp2OrHttp3() => With(Framing.ForHttp2OrHttp3(), Authorization);

        /// <summary>
        /// Makes the same request framed for an h2c upgrade (<see cref="HttpRequestFraming.ForH2cUpgrade" />).
        /// </summary>
        /// <returns>The upgrade request's plan.</returns>
        public HttpRequestPlan ForH2cUpgrade() => With(Framing.ForH2cUpgrade(), Authorization);

        /// <summary>
        /// Makes the same request, marked as sent again on a fresh connection.
        /// </summary>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan OnFreshConnection() => With(Framing, Authorization, sentOnFreshConnection: true, RedirectsFollowed, AuthorizationAnswersChallenge);

        /// <summary>
        /// Makes the same request with <paramref name="authorization" /> as its <c>Authorization</c>
        /// value, its <see cref="UploadRewindFailure" /> kept; this plan when the value is unchanged.
        /// </summary>
        /// <param name="authorization">The value to send; <see langword="null" /> for none.</param>
        /// <returns>The request's plan with that value.</returns>
        public HttpRequestPlan WithAuthorizationSentAgain(string? authorization) =>
            authorization == Authorization ? this : With(Framing, authorization).FailingUploadRewind(UploadRewindFailure);

        /// <summary>
        /// Makes the same request starting its connection-bound handshake again with
        /// <paramref name="firstAuthorization" />, the answer sent before any challenge, for a fresh
        /// connection that the earlier leg's answer does not belong to (BL-5121).
        /// </summary>
        /// <param name="firstAuthorization">The first leg's value, such as an NTLM Type 1 message; <see langword="null" /> for none.</param>
        /// <returns>The restarted request's plan.</returns>
        public HttpRequestPlan WithHandshakeRestarted(string? firstAuthorization) =>
            WithAnswer(firstAuthorization, SentOnFreshConnection, RedirectsFollowed, authorizationAnswersChallenge: false, ProxyAuthorization, ProxyAuthorizationAnswersChallenge)
                .FailingUploadRewind(UploadRewindFailure);

        /// <summary>
        /// Makes the same request, sent again over HTTP/1.1 on a new connection because the server
        /// reset its HTTP/2 stream with <c>HTTP_1_1_REQUIRED</c> (<see cref="DowngradedToHttp11" />).
        /// </summary>
        /// <param name="uploadRewindFailure">
        /// The message its <c>-T</c> upload failed to rewind with, so the resend fails before it
        /// connects (<see cref="UploadRewindFailure" />); <see langword="null" /> when it was rewound.
        /// </param>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan AfterHttp11Required(string? uploadRewindFailure)
        {
            HttpRequestPlan retry = With(Framing, Authorization);
            retry.Options = Options with { Version = HttpVersionPreference.Http11 };
            retry.DowngradedToHttp11 = true;
            return retry.FailingUploadRewind(uploadRewindFailure);
        }

        /// <summary>
        /// Makes the same request, sent again on a new connection because the server refused its
        /// HTTP/3 stream, with <see cref="StreamRefusedRetries" /> one higher.
        /// </summary>
        /// <param name="uploadRewindFailure">
        /// The message its <c>-T</c> upload failed to rewind with, so the resend fails before it
        /// connects (<see cref="UploadRewindFailure" />); <see langword="null" /> when it was rewound.
        /// </param>
        /// <returns>The resent request's plan.</returns>
        public HttpRequestPlan AfterStreamRefused(string? uploadRewindFailure)
        {
            HttpRequestPlan retry = With(Framing, Authorization);
            retry.StreamRefusedRetries = StreamRefusedRetries + 1;
            return retry.FailingUploadRewind(uploadRewindFailure);
        }

        /// <summary>
        /// Marks this plan's upload as having failed to rewind with <paramref name="uploadRewindFailure" />
        /// (<see cref="UploadRewindFailure" />), so the request fails with exit 65 before it connects.
        /// </summary>
        /// <param name="uploadRewindFailure">The rewind's failure, or <see langword="null" /> when it was rewound.</param>
        /// <returns>This plan.</returns>
        public HttpRequestPlan FailingUploadRewind(string? uploadRewindFailure)
        {
            UploadRewindFailure = uploadRewindFailure;
            return this;
        }

        /// <summary>
        /// Sets <see cref="HttpAuthRequest.ServerCertificate" /> on <see cref="AuthRequest" /> to
        /// the DER of <paramref name="connect" />'s TLS server certificate, the first of its
        /// <see cref="ConnectResult.PeerCertificates" />, for an <c>https</c> URL, so hand-built
        /// Negotiate sends <c>tls-server-end-point</c> channel bindings as curl 8.18.0 with MIT
        /// does; empty for an <c>http</c> URL, even through an HTTPS proxy, and the proxy's
        /// request never carries one, as curl takes the bindings only from the origin's TLS
        /// (ADR-0341).
        /// </summary>
        /// <param name="connect">The connection the request goes over.</param>
        public void TakeServerCertificateFrom(ConnectResult connect) =>
            AuthRequest = AuthRequest with
            {
                ServerCertificate = Context.Url.Scheme == "https" && connect.PeerCertificates is [var certificate, ..] ? certificate : default,
            };

        /// <summary>
        /// Marks this retry to go out as HTTP/1.0 when <paramref name="head" />, the response it
        /// answers, was HTTP/1.0 (<see cref="HttpRequestOptions.EarlierResponseWasHttp10" />), as
        /// curl 8.21.0 keeps the downgrade on the new connection an HTTP/1.0 server leaves it
        /// (upstream test1071, BL-2086).
        /// </summary>
        /// <param name="head">The response the retry answers.</param>
        /// <returns>This plan.</returns>
        public HttpRequestPlan DowngradedAfter(HttpResponseHead head)
        {
            if (head.StatusLine.Version == HttpVersion.Version10)
            {
                Options = Options with { EarlierResponseWasHttp10 = true };
            }

            return this;
        }

        /// <summary>
        /// Makes the retry that answers a challenge with <paramref name="authorization" /> and
        /// <paramref name="proxyAuthorization" />: with the body any probe held back, or, when
        /// either value is an NTLM Type 1 message, as a probe again holding the body back for
        /// the Type 3 message (<see cref="SendsNtlmProbe" />; upstream test243, BL-2029).
        /// </summary>
        private HttpRequestPlan WithAnswer(
            string? authorization,
            bool sentOnFreshConnection,
            int redirectsFollowed,
            bool authorizationAnswersChallenge,
            string? proxyAuthorization,
            bool proxyAuthorizationAnswersChallenge,
            IReadOnlyList<string>? authorizationInfoLines = null)
        {
            HttpRequestFraming full = ProbedFraming ?? Framing;
            bool probes = SendsNtlmProbe(full, authorization, proxyAuthorization);
            return With(
                probes ? full.AsAuthProbe() : full,
                authorization,
                sentOnFreshConnection,
                redirectsFollowed,
                authorizationAnswersChallenge,
                proxyAuthorization,
                proxyAuthorizationAnswersChallenge,
                authorizationInfoLines,
                endsProbe: true,
                nextProbedFraming: probes ? full : null);
        }

        private HttpRequestPlan With(HttpRequestFraming framing, string? authorization) =>
            With(framing, authorization, SentOnFreshConnection, RedirectsFollowed, AuthorizationAnswersChallenge);

        private HttpRequestPlan With(HttpRequestFraming framing, string? authorization, bool sentOnFreshConnection, int redirectsFollowed, bool authorizationAnswersChallenge) =>
            With(framing, authorization, sentOnFreshConnection, redirectsFollowed, authorizationAnswersChallenge, ProxyAuthorization, ProxyAuthorizationAnswersChallenge);

        private HttpRequestPlan With(
            HttpRequestFraming framing,
            string? authorization,
            bool sentOnFreshConnection,
            int redirectsFollowed,
            bool authorizationAnswersChallenge,
            string? proxyAuthorization,
            bool proxyAuthorizationAnswersChallenge,
            IReadOnlyList<string>? authorizationInfoLines = null,
            HttpTransferException? authorizationFailure = null,
            bool endsProbe = false,
            HttpRequestFraming? nextProbedFraming = null) =>
            new(Context, Options, framing, AuthRequest, authorization)
            {
                ProbedFraming = endsProbe ? nextProbedFraming : ProbedFraming,
                Started = Started,
                Deadline = Deadline,
                Progress = Progress,
                ForwardProxy = ForwardProxy,
                ProxyAuthRequest = ProxyAuthRequest,
                ProxyAuthorization = proxyAuthorization,
                ProxyAuthorizationAnswersChallenge = proxyAuthorizationAnswersChallenge,
                ProxyAuthorizationInfoLines = proxyAuthorizationAnswersChallenge ? [] : ProxyAuthorizationInfoLines,
                SentOnFreshConnection = sentOnFreshConnection,
                RedirectsFollowed = redirectsFollowed,
                AuthorizationAnswersChallenge = authorizationAnswersChallenge,
                AuthorizationInfoLines = authorizationInfoLines ?? AuthorizationInfoLines,
                NtlmType3WentOut = SendsNothingAfterNtlmType3(NtlmType3WentOut, Authorization, authorization),
                ProxyNtlmType3WentOut = SendsNothingAfterNtlmType3(ProxyNtlmType3WentOut, ProxyAuthorization, proxyAuthorization),
                AuthorizationFailure = authorizationFailure ?? AuthorizationFailure,
                StreamRefusedRetries = StreamRefusedRetries,
                DowngradedToHttp11 = DowngradedToHttp11,
                HoldsDigestNonce = HoldsDigestNonce,
                RereadsUrl = RereadsUrl,
                AuthSchemesOffered = AuthSchemesOffered,
                ProxyAuthSchemesOffered = ProxyAuthSchemesOffered,
            };
    }
}
