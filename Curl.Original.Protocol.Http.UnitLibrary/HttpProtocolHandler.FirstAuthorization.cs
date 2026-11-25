using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that builds the authorization requests of a transfer and the Authorization values its first request carries.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Makes the authentication request for the origin server of the transfer's first request.
    /// </summary>
    private static HttpAuthRequest OriginAuthRequestOf(ITransferContext context, HttpRequestOptions options, HttpRequestFraming framing) =>
        new(
            framing.Method,
            context.Url,
            options.RequestTarget ?? HttpUrlText.OriginTarget(context.Url, options.CommandLineTextEncoding),
            OriginCredentialOf(context, options),
            options.BearerToken,
            AllowedSchemesOf(options),
            IsProxy: false)
        {
            Events = context.Events,
            AwsSigV4 = AwsSigV4InputsOf(context, options, framing),
            HttpSignature = HttpSignatureInputsOf(context, options),
        };

    /// <summary>
    /// Gives the credential the origin is answered with: the transfer's, else an empty user name
    /// and password when <c>--sasl-authzid</c> is given and Digest is the one scheme allowed, as
    /// curl 8.22.0 then probes and answers Digest for user <c>''</c> (AF-0522, BL-4457, ADR-1182),
    /// or when <c>--service-name</c> is given and NTLM is the one scheme allowed, as curl 8.22.0
    /// then starts NTLM with the default Windows credentials on Windows only (AF-0524, BL-4460,
    /// BL-4509, ADR-1183), or when <c>--service-name</c> is given with <c>--aws-sigv4</c>, which
    /// curl signs with an empty key ID on every platform (AF-0923, BL-5658);
    /// <see langword="null" /> otherwise.
    /// </summary>
    private static NetworkCredential? OriginCredentialOf(ITransferContext context, HttpRequestOptions options) =>
        context.Credentials
            ?? (StartsWithAnEmptyUser(options, OperatingSystem.IsWindows()) ? new NetworkCredential(string.Empty, string.Empty) : null);

    /// <summary>
    /// Tells whether the platform's curl answers the origin as an empty user when the transfer has
    /// no credential, in three cases:
    /// <list type="bullet">
    /// <item><description><c>--sasl-authzid</c> with Digest the one scheme allowed, on every platform:
    /// curl probes and answers Digest for user <c>''</c> (ADR-1182).</description></item>
    /// <item><description><c>--service-name</c> with NTLM the one scheme allowed, on Windows only:
    /// the Schannel build starts NTLM with the default Windows credentials, while the OpenSSL
    /// build sends no <c>Authorization</c> (ADR-1183).</description></item>
    /// <item><description><c>--service-name</c> with <c>--aws-sigv4</c>, on every platform: curl
    /// signs the request with an empty key ID (AF-0923, BL-5658).</description></item>
    /// </list>
    /// </summary>
    /// <param name="options">The transfer's HTTP options.</param>
    /// <param name="onWindows">Whether Curl runs on Windows, where curl is the Schannel build.</param>
    internal static bool StartsWithAnEmptyUser(HttpRequestOptions options, bool onWindows) =>
        (options.AwsSigV4 is not null && options.ServiceName is not null) || AllowedSchemesOf(options) switch
        {
            HttpAuthSchemes.Digest => options.SaslAuthorizationIdentity is not null,
            HttpAuthSchemes.Ntlm => onWindows && options.ServiceName is not null,
            _ => false,
        };

    /// <summary>
    /// Makes the authentication request for a forward proxy, or gives <see langword="null" />
    /// when there is none.
    /// </summary>
    private HttpAuthRequest? ProxyAuthRequestOrNull(HttpAuthRequest authRequest, ProxyEndpoint? forwardProxy) =>
        forwardProxy is null ? null : ProxyAuthRequestOf(authRequest, forwardProxy);

    /// <summary>
    /// Decides whether the transfer's first request goes as a probe with an empty body: a Digest
    /// one to the origin or the proxy (<see cref="SendsDigestProbe" />), or one that starts an
    /// NTLM handshake (<see cref="SendsNtlmProbe" />).
    /// </summary>
    private static bool SendsFirstProbe(HttpRequestFraming framing, HttpAuthRequest authRequest, string? authorization, HttpAuthRequest? proxyAuthRequest, string? proxyAuthorization) =>
        SendsDigestProbe(framing, authRequest, authorization)
            || (proxyAuthRequest is not null && SendsDigestProbe(framing, proxyAuthRequest, proxyAuthorization))
            || SendsNtlmProbe(framing, authorization, proxyAuthorization);

    /// <summary>
    /// Decides whether the first request goes as a probe with an empty body
    /// (<see cref="HttpRequestFraming.AsAuthProbe" />): one with a body, sent where Digest is the
    /// one scheme allowed, with credentials and no value made yet, as curl 8.21.0 holds a POST's
    /// or PUT's body back until the Digest challenge is answered (upstream test88, test175,
    /// test1001; ADR-0441).
    /// </summary>
    private static bool SendsDigestProbe(HttpRequestFraming framing, HttpAuthRequest request, string? authorization) =>
        framing.Body is not null
            && request.AllowedSchemes == HttpAuthSchemes.Digest
            && request.Credential is not null
            && authorization is null;

    /// <summary>
    /// Decides whether a request goes as a probe with an empty body
    /// (<see cref="HttpRequestFraming.AsAuthProbe" />) because it starts an NTLM handshake: one
    /// with a body whose <c>Authorization</c> or <c>Proxy-Authorization</c> value is an NTLM
    /// Type 1 message, as curl 8.21.0 holds a POST's or PUT's body back until the Type 3 message
    /// goes (upstream test170, test176, test239, test243, test267; BL-2029).
    /// </summary>
    private static bool SendsNtlmProbe(HttpRequestFraming framing, string? authorization, string? proxyAuthorization) =>
        framing.Body is not null && (IsNtlmType1(authorization) || IsNtlmType1(proxyAuthorization));

    /// <summary>
    /// Decides whether an <c>Authorization</c> or <c>Proxy-Authorization</c> value is an NTLM
    /// Type 1 message: <c>NTLM</c> and the base64 of <c>NTLMSSP\0</c> followed by the message
    /// type 1, whose first twelve bytes encode to exactly <see cref="NtlmType1Prefix" />.
    /// </summary>
    private static bool IsNtlmType1(string? value) =>
        value?.StartsWith(NtlmType1Prefix, StringComparison.Ordinal) is true;

    /// <summary>
    /// The start of every NTLM Type 1 value: <c>NTLM </c> and the base64 of the message's
    /// signature <c>NTLMSSP\0</c> and its type, 1, as a little-endian 32-bit number.
    /// </summary>
    private const string NtlmType1Prefix = "NTLM TlRMTVNTUAABAAAA";

    /// <summary>
    /// Gives the schemes the origin request may answer with: the scheme an earlier hop's server
    /// picked (<see cref="HttpRequestOptions.AuthSchemePicked" />) alone when it is one of
    /// <see cref="HttpRequestOptions.AuthSchemes" />, as libcurl 8.21.0 keeps its picked scheme
    /// across redirects and so sends Basic before any challenge (upstream test1088, BL-1819);
    /// otherwise every scheme allowed. NTLM and Negotiate are not kept: they authenticate a
    /// connection, so the first request of the next hop carries no <c>Authorization</c> and
    /// probes again (upstream test90, BL-2059; measured on curl 8.21.0 with <c>--anyauth -L</c>).
    /// </summary>
    private static HttpAuthSchemes AllowedSchemesOf(HttpRequestOptions options) =>
        options.AuthSchemePicked is not (HttpAuthSchemes.None or HttpAuthSchemes.Ntlm or HttpAuthSchemes.Negotiate)
        && (options.AuthSchemes & options.AuthSchemePicked) == options.AuthSchemePicked
            ? options.AuthSchemePicked
            : options.AuthSchemes;

    /// <summary>
    /// Makes the Digest value an earlier hop sent (<see cref="HttpRequestOptions.KeptDigestAuthorization" />)
    /// again for this hop's request, its nonce counted on by
    /// <see cref="IHttpAuthenticator.RepeatAuthorization" />, as curl 8.22.0 sends <c>nc=00000002</c>
    /// to a same-host redirect after answering a Digest challenge (upstream test1286, BL-2252);
    /// <see langword="null" /> when no value was kept, there is no credential or Digest is not allowed.
    /// </summary>
    private string? RepeatKeptDigestAuthorization(HttpRequestOptions options, HttpAuthRequest request) =>
        options.KeptDigestAuthorization is { } kept
        && request.Credential is not null
        && (request.AllowedSchemes & HttpAuthSchemes.Digest) != 0
            ? Authenticator.RepeatAuthorization(request, kept)
            : null;

    /// <summary>
    /// Makes the first request's Digest value from what the transfer kept: the value an earlier hop
    /// sent, counted on (<see cref="RepeatKeptDigestAuthorization" />), else the answer to the
    /// challenges the handle's previous transfer kept (<see cref="AnswerKeptDigestChallengesAsync" />),
    /// with whether it is that answer (<see cref="HttpRequestPlan.AuthorizationAnswersKeptDigestChallenges" />).
    /// </summary>
    private async ValueTask<(string? Authorization, bool AnswersKeptChallenges)> KeptDigestAuthorizationAsync(HttpRequestOptions options, HttpAuthRequest request, CancellationToken cancellationToken)
    {
        if (RepeatKeptDigestAuthorization(options, request) is { } repeated)
        {
            return (repeated, false);
        }

        string? answer = await AnswerKeptDigestChallengesAsync(options, request, cancellationToken).ConfigureAwait(false);
        return (answer, answer is not null);
    }

    /// <summary>
    /// Answers the challenges a 401 of the handle's previous transfer offered
    /// (<see cref="HttpRequestOptions.KeptDigestChallenges" />) on this transfer's first request,
    /// as curl 8.22.0 builds its Digest answer from the nonce it kept on the easy handle when
    /// Digest is the one scheme <c>CURLOPT_HTTPAUTH</c> now allows (upstream test2024, BL-2747,
    /// ADR-0692); <see langword="null" /> when none was kept, there is no credential or Digest is
    /// not the one scheme allowed.
    /// </summary>
    private async ValueTask<string?> AnswerKeptDigestChallengesAsync(HttpRequestOptions options, HttpAuthRequest request, CancellationToken cancellationToken) =>
        options.KeptDigestChallenges.Count > 0
        && request.Credential is not null
        && request.AllowedSchemes == HttpAuthSchemes.Digest
            ? await Authenticator.CreateAuthorizationAsync(request, [.. options.KeptDigestChallenges], cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>
    /// Gives the <c>Authorization</c> value <paramref name="plan" /> sends when it is a Digest
    /// one, the report's <see cref="TransferReport.DigestAuthorizationSent" /> (BL-2252);
    /// <see langword="null" /> otherwise.
    /// </summary>
    private static string? DigestAuthorizationSentBy(HttpRequestPlan plan) =>
        SchemeOfAuthorization(plan.Authorization) == HttpAuthSchemes.Digest ? plan.Authorization : null;

    /// <summary>
    /// Gives the scheme a server picked for the request <paramref name="plan" /> sends: the
    /// scheme of an <c>Authorization</c> value that answers a challenge, or else the one the
    /// transfer's earlier hop picked (BL-1819).
    /// </summary>
    private static HttpAuthSchemes AuthSchemePickedBy(HttpRequestPlan plan) =>
        SchemeOfAuthorization(plan.Authorization) is var scheme && scheme != HttpAuthSchemes.None
        && (plan.AuthorizationAnswersChallenge || scheme == HttpAuthSchemes.Basic)
            ? scheme
            : plan.Options.AuthSchemePicked;

    /// <summary>
    /// Gives every scheme <paramref name="head" />'s <paramref name="headerName" /> challenges
    /// offer when its status is <paramref name="statusCode" />, the only status libcurl reads
    /// them on; <see cref="HttpAuthSchemes.None" /> for any other status.
    /// </summary>
    private static HttpAuthSchemes SchemesChallengedBy(HttpResponseHead head, int statusCode, string headerName) =>
        head.StatusLine.StatusCode == statusCode
            ? ValuesOf(head, headerName).Aggregate(HttpAuthSchemes.None, (schemes, challenge) => schemes | SchemeOfAuthorization(challenge))
            : HttpAuthSchemes.None;

    /// <summary>
    /// Gives the scheme an <c>Authorization</c> value starts with, or
    /// <see cref="HttpAuthSchemes.None" /> for none or one this handler does not pick.
    /// </summary>
    private static HttpAuthSchemes SchemeOfAuthorization(string? authorization) =>
        authorization?.Split(' ', 2)[0] switch
        {
            "Basic" => HttpAuthSchemes.Basic,
            "Digest" => HttpAuthSchemes.Digest,
            "NTLM" => HttpAuthSchemes.Ntlm,
            "Negotiate" => HttpAuthSchemes.Negotiate,
            _ => HttpAuthSchemes.None,
        };

    /// <summary>
    /// Makes the first request's <c>Proxy-Authorization</c> value for a forward proxy, the
    /// authenticator's lines going to <paramref name="lines" />; <see langword="null" /> when
    /// there is no forward proxy.
    /// </summary>
    private async ValueTask<string?> CreateFirstProxyAuthorizationAsync(HttpAuthRequest? proxyAuthRequest, HttpInfoLineRecorder lines, CancellationToken cancellationToken) =>
        proxyAuthRequest is null
            ? null
            : await Authenticator.CreateAuthorizationAsync(proxyAuthRequest with { Events = lines }, [], cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gives a failed transfer the message of the first line the authenticator reported while
    /// it made the first request's <c>Authorization</c> value, a Negotiate context's failure, as
    /// curl 8.21.0 does: its <c>failf</c> for the context fills the error buffer before any later
    /// one, so <c>curl: (22)</c> after <c>-f</c> meets the 401 carries it rather than
    /// <c>The requested URL returned error: 401</c> (measured, BL-955 Notes; ADR-0344).
    /// </summary>
    /// <remarks>
    /// An <c>--aws-sigv4</c> transfer keeps its own message: every line its signer reports is a
    /// <c>-v</c> info line, never a failure, so the string to sign never becomes the error
    /// message (BL-1454). A transfer whose first connection never opened keeps its own message
    /// too: curl makes the context only once connected, so a refused connect or a failed
    /// <c>--interface</c> bind never meets it (measured, BL-1684).
    /// </remarks>
    private static TransferResult WithFirstAuthorizationFailure(TransferResult result, HttpAuthRequest request, IReadOnlyList<string> authorizationLines) =>
        result.ExitCode != CurlExitCode.Ok && request.AwsSigV4 is null && request.HttpSignature is null && authorizationLines.Count > 0
            ? result with { ErrorMessage = authorizationLines[0] }
            : result;

    /// <summary>
    /// Makes the request the authenticator is asked about for the forward proxy: the origin's
    /// method and request target (Digest's <c>uri</c> is the origin form, as curl 8.21.0
    /// sends it), the proxy's own URL (NTLM and Negotiate ask for <c>HTTP</c> on the proxy's
    /// host, as curl's Type 3 names <c>HTTP/&lt;proxy&gt;</c>, BL-604 Notes), the proxy's
    /// credential, no bearer token, and <see cref="ProxyAuthSchemes" />. Answered with no
    /// challenges it gives the pre-emptive <c>Proxy-Authorization</c>: Basic's, or NTLM's Type 1
    /// or Negotiate's first token when that is the one scheme allowed (ADR-0270).
    /// </summary>
    private HttpAuthRequest ProxyAuthRequestOf(HttpAuthRequest originRequest, ProxyEndpoint proxy) =>
        originRequest with { Url = ProxyUrlOf(proxy), Credential = proxy.Credential, BearerToken = null, AllowedSchemes = ProxyAuthSchemes, IsProxy = true, AwsSigV4 = null, HttpSignature = null };

    /// <summary>
    /// Gives <paramref name="proxy" />'s own URL, <c>http://host:port/</c> (<c>https</c> for an
    /// HTTPS proxy), an IPv6 literal in brackets.
    /// </summary>
    private static CurlUrl ProxyUrlOf(ProxyEndpoint proxy)
    {
        string host = proxy.Host.Contains(':', StringComparison.Ordinal) && !proxy.Host.StartsWith('[') ? $"[{proxy.Host}]" : proxy.Host;
        return CurlUrl.Parse($"{(proxy.Kind == ProxyKind.Https ? "https" : "http")}://{host}:{proxy.Port}/");
    }

    /// <summary>
    /// Gives what signing the request with <c>--aws-sigv4</c> needs, or <see langword="null" />
    /// without it: the <c>Host</c> value curl's own head carries, the <c>-H</c> headers, the
    /// <c>-d</c> body, the <c>-T</c> upload's size and <c>--path-as-is</c> (BL-629).
    /// </summary>
    private static AwsSigV4Inputs? AwsSigV4InputsOf(ITransferContext context, HttpRequestOptions options, HttpRequestFraming framing) =>
        options.AwsSigV4 is { } parameter
            ? new AwsSigV4Inputs(parameter, HttpUrlText.HostHeaderAuthority(context.Url), options.Headers)
            {
                PostFields = options.Body is BytesBody postFields ? (ReadOnlyMemory<byte>?)postFields.Content : null,
                UploadSize = framing.IsUpload ? framing.KnownLength ?? -1 : -1,
                IsGetOrHead = framing.Body is null && !framing.IsUpload,
                PathAsIs = context.PathAsIs,
            }
            : null;

    /// <summary>
    /// Gives what signing the request with the <c>--httpsig-*</c> options needs, or
    /// <see langword="null" /> without them: the settings, the <c>@authority</c> value - an
    /// <c>-H</c> <c>Host</c> value when one is sent, else the <c>Host</c> value curl's own head
    /// carries - and the <c>-H</c> headers (BL-2182).
    /// </summary>
    private static HttpSignatureInputs? HttpSignatureInputsOf(ITransferContext context, HttpRequestOptions options) =>
        options.HttpSignature is { } settings
            ? new HttpSignatureInputs(settings, HttpRequestHeadFormatter.CustomHostOf(options) ?? HttpUrlText.HostHeaderAuthority(context.Url), options.Headers)
            : null;

    /// <summary>
    /// Asks the authenticator for the value the transfer's first request is sent with, before
    /// any challenge; a refusal (<see cref="HttpAuthenticationFailedException" />) is given
    /// back instead, to fail the exchange once connected, where curl 8.21.0 signs with
    /// <c>--aws-sigv4</c> and reports its failure (measured, BL-629 Notes).
    /// </summary>
    private async ValueTask<(string? Authorization, HttpTransferException? Failure)> CreateFirstAuthorizationAsync(HttpAuthRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return (await Authenticator.CreateAuthorizationAsync(request, [], cancellationToken).ConfigureAwait(false), null);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            return (null, new HttpTransferException(failure.ExitCode, failure.Message));
        }
    }
}
