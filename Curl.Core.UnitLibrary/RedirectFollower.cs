using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Curl.Core.Hsts;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Performs a transfer through <see cref="ProtocolDispatcher" /> and, under
/// <c>-L</c>/<c>--location</c>, follows each <see cref="TransferReport.RedirectUrl" /> with
/// curl 8.21.0's redirect limit, method rewriting and credential rules, returning the last
/// hop's result with one report merged across every hop.
/// </summary>
/// <remarks>
/// <para>
/// A hop is followed when it succeeded with a 3xx status and a redirect URL. Before
/// following, the limit is checked (exit 47, <c>Maximum (N) redirects followed</c>), then
/// whether the target parses (exit 3, <c>The redirect target URL could not be parsed:
/// Malformed input to a URL function</c> for a control byte or DEL, <c>Bad IPv6 address</c>, <c>Bad hostname</c> or the port reason), then the target's scheme: one this curl build cannot parse fails with exit 1,
/// <c>The redirect target URL could not be parsed: Unsupported URL scheme</c>, and one
/// <see cref="RedirectPolicy.AllowedSchemes" /> (<c>--proto-redir</c>) or
/// <see cref="RedirectPolicy.AllowedTransferSchemes" /> (<c>--proto</c>) does not allow with exit 1,
/// <c>Protocol "file" is disabled (in redirect)</c>. The first URL's scheme is checked against
/// <see cref="RedirectPolicy.AllowedTransferSchemes" /> by <see cref="ProtocolDispatcher" />, with or
/// without <c>-L</c> (measured, BL-523 Notes).
/// </para>
/// <para>
/// A POST body is dropped, making the request a GET, on 301 and 302 unless
/// <c>--post301</c> or <c>--post302</c>, and on 303 unless <c>--post303</c>; 303 also drops a
/// <c>-T</c> upload. A <c>-X</c> method is kept, as curl keeps it. Once dropped, a body
/// stays dropped for every later hop. A body that is kept - a <c>-T</c> upload or a <c>-F</c>
/// <see cref="StreamBody" /> - is sent again from its start when its stream seeks, as curl
/// 8.21.0 sends the same multipart body, boundary and all, after a 307 or 308 (BL-298 Notes).
/// A kept <see cref="StreamBody" /> whose stream cannot seek - a file part read from a pipe or
/// device - ends the chain at the next hop, counting the redirect and leaving
/// <c>%{redirect_url}</c> empty: on Windows with exit 26, <c>read error getting mime data</c>,
/// as the Schannel build does (measured, BL-359 Notes), elsewhere with exit 65,
/// <c>Cannot rewind mime/post data</c>, as the OpenSSL build does (measured, BL-402 Notes;
/// ADR-0098).
/// </para>
/// <para>
/// A hop whose host, port or scheme differs from the first URL's gets no
/// <see cref="ITransferContext.Credentials" />, no bearer token and no <c>-H</c>
/// <c>Authorization:</c> or <c>Cookie:</c> header, unless <c>--location-trusted</c>.
/// </para>
/// <para>
/// Credentials written in a URL belong to that URL, as curl 8.21.0's do (measured, BL-814
/// Notes): each hop sends its own URL's user name and password - a relative <c>Location</c>
/// keeps the first URL's, an absolute one brings its own or none - unless the first hop's
/// <see cref="ITransferContext.Credentials" /> came from elsewhere (<c>-u</c>, netrc) and are
/// sent to this hop, in which case they win. First-hop credentials equal to the first URL's
/// own are taken as the URL's.
/// </para>
/// <para>
/// Every hop after the first goes through the proxy <see cref="HopProxySelector" /> chooses for
/// that hop's own URL, set as both <see cref="ITransferContext.Proxy" /> and
/// <see cref="HttpRequestOptions.ForwardProxy" />, as curl 8.21.0 chooses again for each hop: a
/// redirect to a <c>--noproxy</c> host, or from <c>http</c> to <c>https</c> with only
/// <c>http_proxy</c> set, goes direct (measured, BL-329 Notes). A selector failure ends the
/// chain with that failure. Without a selector, every hop keeps the first URL's proxy.
/// </para>
/// <para>
/// Under <see cref="HttpRequestOptions.AutoReferer" /> (<c>-e "...;auto"</c>) every hop after the
/// first is sent the previous hop's URL, without user information or fragment, as its
/// <c>Referer</c>, and the merged report's <see cref="TransferReport.Referer" /> is the one the
/// last request was sent with (measured, BL-361 Notes; ADR-0101).
/// </para>
/// <para>
/// Each hop's own <see cref="TransferReport.RedirectCount" /> - the HTTP handler's resends after
/// a 417 - is added to the chain's count, and every hop after the first is sent that count as
/// <see cref="HttpRequestOptions.RedirectsFollowed" />, so the resends and the followed hops share
/// one <c>--max-redirs</c> limit, as curl 8.21.0's do (measured, BL-396 Notes).
/// </para>
/// <para>
/// Every hop after the first is sent the previous hop's
/// <see cref="TransferReport.ResponseHeadersStored" /> as
/// <see cref="HttpRequestOptions.ResponseHeadersStored" />, so the hops' headers share one limit
/// of 5000, as curl 8.21.0's do (measured, BL-1448 Notes).
/// </para>
/// <para>
/// Every hop after the first is sent the previous hop's
/// <see cref="TransferReport.AuthSchemePicked" /> as
/// <see cref="HttpRequestOptions.AuthSchemePicked" />, so a scheme <c>--anyauth</c> settled on
/// is sent to the next hop before any challenge, as libcurl 8.21.0 does (upstream test1088, BL-1819).
/// </para>
/// <para>
/// Every hop that may be sent credentials is sent the previous hop's
/// <see cref="TransferReport.DigestAuthorizationSent" /> as
/// <see cref="HttpRequestOptions.KeptDigestAuthorization" />, so a Digest answer goes to the next
/// hop again with its nonce counted on, as curl 8.22.0 sends <c>nc=00000002</c> after a same-host
/// redirect (upstream test1286, BL-2252); a hop that may not is sent none.
/// </para>
/// <para>
/// Every hop after one whose response was HTTP/1.0 is sent
/// <see cref="HttpRequestOptions.EarlierResponseWasHttp10" />, so it goes out as HTTP/1.0, as
/// curl 8.21.0 sends every later request of a transfer once a response in it was HTTP/1.0: a
/// <c>-T -</c> upload then fails the hop with exit 25 before sending it (upstream test1073, BL-2037).
/// </para>
/// <para>
/// Every hop after the first carries the chain's start as
/// <see cref="ITransferContext.OperationStarted" />, so <c>-m</c> limits the whole chain, as
/// curl's does, rather than each hop (measured, BL-299 Notes).
/// </para>
/// </remarks>
/// <param name="dispatcher">The dispatcher that performs each hop.</param>
/// <param name="selectHopProxy">
/// Chooses the proxy for each hop after the first, or <see langword="null" /> to keep the first
/// URL's proxy for every hop.
/// </param>
/// <param name="runsOnWindows">
/// Whether an unseekable body's rewind fails as the Windows build fails it (exit 26) rather
/// than as the Linux and macOS build does (exit 65); <see langword="null" /> for
/// <see cref="OperatingSystem.IsWindows" />.
/// </param>
/// <param name="hsts">
/// The run's HSTS cache, which switches every <c>http</c>
/// redirect target it knows to <c>https</c> before the target's scheme is checked, reporting
/// <see cref="HstsTransferPolicy.SwitchedMessagePrefix" /> and the URL to the hop's events, as
/// curl 8.21.0 does after <c>Issue another request to this URL</c> (BL-621 Notes);
/// <see langword="null" /> for none.
/// </param>
/// <param name="selectHopAltSvc">
/// Looks up each hop's <c>--alt-svc</c> alternative and HTTP version from the hop's own URL, as
/// curl 8.21.0 looks it up for each connection; <see langword="null" /> to keep the first hop's
/// <see cref="HttpRequestOptions.AltSvcRoute" /> for a hop to the first URL's origin and drop it
/// for any other.
/// </param>
/// <param name="selectHopCredentials">
/// Looks up each hop's <see cref="ITransferContext.Credentials" /> from the hop's own URL, whatever
/// its origin and <c>--location-trusted</c>, as curl 8.21.0 looks the netrc file up again for each
/// hop's host (BL-790 Notes); <see langword="null" /> to carry the first hop's credentials by the
/// origin and URL rules above.
/// </param>
public sealed class RedirectFollower(
    ProtocolDispatcher dispatcher,
    HopProxySelector? selectHopProxy = null,
    bool? runsOnWindows = null,
    HstsTransferPolicy? hsts = null,
    HopAltSvcSelector? selectHopAltSvc = null,
    HopCredentialSelector? selectHopCredentials = null)
{
    /// <summary>The Linux and macOS build's message for a multipart body it cannot rewind for the next hop.</summary>
    public const string CannotRewindMessage = "Cannot rewind mime/post data";

    /// <summary>
    /// curl 8.21.0's message, with exit 67, for a URL with user information under
    /// <c>--disallow-username-in-url</c> (BL-626 Notes).
    /// </summary>
    public const string CredentialsInUrlMessage = "URL rejected: Credentials was passed in the URL when prohibited";

    /// <summary>
    /// The start of curl 8.21.0's <c>-v</c> line for a redirect target it goes on to request, followed
    /// by the target and a closing <c>'</c>: written once the target parses with a scheme curl knows,
    /// before the HSTS switch and the <c>--proto-redir</c> and <c>--disallow-username-in-url</c>
    /// checks, and not when <c>--max-redirs</c> refuses the hop (measured, BL-907 Notes).
    /// </summary>
    public const string IssueAnotherRequestMessagePrefix = "Issue another request to this URL: '";

    private readonly bool rewindFailsAsReadError = runsOnWindows ?? OperatingSystem.IsWindows();

    // curl's Windows build reads a single letter and a colon as a drive prefix, not a scheme.
    private readonly bool driveLetterIsNotScheme = runsOnWindows ?? OperatingSystem.IsWindows();

    private static readonly HashSet<string> SchemesCurlParses = new(
        [
            "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap",
            "imaps", "ldap", "ldaps", "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smb", "smbs",
            "smtp", "smtps", "telnet", "tftp", "ws", "wss",
        ],
        StringComparer.Ordinal);

    private static readonly System.Buffers.SearchValues<char> SchemeCharacters =
        System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+-.");

    /// <summary>
    /// Performs the transfer, following redirects when
    /// <see cref="HttpRequestOptions.FollowRedirects" /> is set.
    /// </summary>
    /// <param name="context">The transfer as given on the command line.</param>
    /// <param name="policy">The redirect options.</param>
    /// <returns>
    /// Without <c>-L</c>, the dispatcher's result unchanged. With it, the last hop's
    /// result, or the refusal that stopped the chain, carrying the last hop's report with
    /// <see cref="TransferReport.RedirectCount" />, <see cref="TransferReport.EffectiveUrl" />
    /// (the last URL requested, <see langword="null" /> when no redirect was followed),
    /// header, request and connection counts summed over every hop, and
    /// <see cref="TransferReport.Timings" /> merged by <see cref="RedirectHopTimings" />: measured from
    /// the first hop's start, each phase the sum of its time on every hop, and
    /// <see cref="TransferTimings.RedirectDuration" /> the time until the last hop began.
    /// </returns>
    public ValueTask<TransferResult> FollowAsync(ITransferContext context, RedirectPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);

        return context.Http is { FollowRedirects: true } http
            ? FollowChainAsync(context, http, policy)
            : DispatchAsync(context, policy);
    }

    /// <summary>Performs one hop.</summary>
    private ValueTask<TransferResult> DispatchAsync(ITransferContext hop, RedirectPolicy policy) =>
        dispatcher.DispatchAsync(hop, policy.AllowedTransferSchemes);

    private async ValueTask<TransferResult> FollowChainAsync(
        ITransferContext context,
        HttpRequestOptions http,
        RedirectPolicy policy)
    {
        RedirectChain chain = new(context.TimeProvider);
        RedirectDiagnosticLog log = new(context.DiagnosticLog);
        long operationStarted = context.OperationStarted ?? context.TimeProvider.GetTimestamp();
        long? uploadStart = SeekableStart(context.Upload);
        Stream? bodyContent = StreamBodyContent(http);
        long? bodyStart = SeekableStart(bodyContent);
        ITransferContext hop = context;
        bool bodyDropped = false;
        bool methodDropped = false;
        bool receivedHttp10 = false;
        while (true)
        {
            TransferResult result = await DispatchAsync(hop, policy);
            chain.Add(result.Report, hop.Http!.Referer);
            if (RedirectTarget(result) is not { } redirectUrl)
            {
                return chain.Merge(result);
            }

            string target = chain.Targeted(redirectUrl);

            int responseCode = result.Report!.ResponseCode;
            receivedHttp10 |= result.Report.HttpVersion == HttpVersion.Version10;
            string? methodSwitchMessage = MethodSwitchMessage(responseCode, hop, http.CustomMethod, policy);
            bodyDropped |= DropsBody(responseCode, hop, policy);
            methodDropped |= DropsCustomMethod(responseCode, hop, policy, bodyDropped);
            bool bodyCannotBeResent = CannotBeResent(bodyContent, bodyDropped);
            log.Limit(policy.MaxRedirects, chain.RedirectCount);
            if (StopBeforeHop(context, http, ref target, methodSwitchMessage, chain, policy, result, bodyCannotBeResent, out CurlUrl? next, out HopProxy hopProxy) is { } stop)
            {
                log.Refused(responseCode, target, stop);
                return chain.Merge(stop);
            }

            string? seekFailure = SeekUploadToStart(context.Upload, uploadStart, bodyDropped);
            Rewind(bodyContent, bodyStart, bodyDropped);
            log.Followed(responseCode, target, bodyDropped, methodDropped);
            chain.Followed(target);
            if (UploadRewindFailure(context, seekFailure, uploadStart, bodyDropped, result) is { } rewindFailure)
            {
                chain.Refused(keepsRedirectUrl: false);
                return chain.Merge(rewindFailure);
            }

            // No stop means the target parsed, so next is set.
            hop = NextHop(context, hop.Url, next!, HopMethod(http, methodDropped, policy) with { RedirectsFollowed = chain.RedirectCount, ResponseHeadersStored = result.Report.ResponseHeadersStored, AuthSchemePicked = result.Report.AuthSchemePicked, KeptDigestAuthorization = result.Report.DigestAuthorizationSent, EarlierResponseWasHttp10 = receivedHttp10 }, hopProxy, bodyDropped, policy.LocationTrusted || IsSameOrigin(context.Url, next!), operationStarted, selectHopAltSvc, selectHopCredentials);
        }
    }

    /// <summary>
    /// The failure that ends the chain instead of following <paramref name="target" />: a
    /// <see cref="Refusal" />, the hop proxy selector's failure, or - when
    /// <paramref name="bodyCannotBeResent" /> - curl's failure for a multipart body it cannot
    /// rewind, which counts the redirect as followed; <see langword="null" />, with
    /// <paramref name="next" /> and <paramref name="hopProxy" /> set, when the hop goes ahead.
    /// A target the HSTS cache switches to <c>https</c> comes back switched in <paramref name="target" />.
    /// </summary>
    private TransferResult? StopBeforeHop(
        ITransferContext first,
        HttpRequestOptions http,
        ref string target,
        string? methodSwitchMessage,
        RedirectChain chain,
        RedirectPolicy policy,
        TransferResult result,
        bool bodyCannotBeResent,
        out CurlUrl? next,
        out HopProxy hopProxy)
    {
        hopProxy = default;
        if (Refusal(ref target, methodSwitchMessage, first, chain.RedirectCount, policy, out next) is { } refusal)
        {
            if (!refusal.KeepsRedirectUrl)
            {
                chain.CountRefused(refusal.ReachesTarget ? target : null);
            }

            chain.Refused(refusal.KeepsRedirectUrl);
            return TransferResult.Failure(refusal.ExitCode, refusal.Message, result.BytesTransferred);
        }

        // No refusal means the target parsed, so next is set.
        return UserInUrlFailure(target, next!, chain, policy, result)
            ?? HopProxyOrRewindFailure(first, http, target, next!, chain, result, bodyCannotBeResent, out hopProxy);
    }

    /// <summary>
    /// <see cref="CredentialsInUrlFailure" /> when <c>--disallow-username-in-url</c> meets a
    /// target with user information; <see langword="null" /> otherwise.
    /// </summary>
    private static TransferResult? UserInUrlFailure(string target, CurlUrl next, RedirectChain chain, RedirectPolicy policy, TransferResult result) =>
        policy.DisallowsUserInUrl && next.User is not null ? CredentialsInUrlFailure(target, chain, result) : null;

    /// <summary>
    /// The hop proxy selector's failure, else <see cref="BodyRewindFailure" /> when
    /// <paramref name="bodyCannotBeResent" />; <see langword="null" />, with <paramref name="hopProxy" />
    /// set, when the hop goes ahead.
    /// </summary>
    private TransferResult? HopProxyOrRewindFailure(
        ITransferContext first,
        HttpRequestOptions http,
        string target,
        CurlUrl next,
        RedirectChain chain,
        TransferResult result,
        bool bodyCannotBeResent,
        out HopProxy hopProxy)
    {
        if (!TrySelectHopProxy(first, http, next, out hopProxy, out TransferResult? failure))
        {
            return failure;
        }

        return bodyCannotBeResent ? BodyRewindFailure(target, chain, result) : null;
    }

    /// <summary>
    /// curl's failure for a redirect target with user information under
    /// <c>--disallow-username-in-url</c>: the redirect counts as followed, <c>%{url_effective}</c> is the
    /// target, <c>%{redirect_url}</c> is empty, and nothing more is sent (measured, BL-626 Notes).
    /// </summary>
    private static TransferResult CredentialsInUrlFailure(string target, RedirectChain chain, TransferResult result)
    {
        chain.Followed(target);
        chain.Refused(keepsRedirectUrl: false);
        return TransferResult.Failure(CurlExitCode.LoginDenied, CredentialsInUrlMessage, result.BytesTransferred);
    }

    /// <summary>
    /// curl's failure for a hop whose multipart body cannot be rewound: the redirect counts as
    /// followed, <c>%{redirect_url}</c> is empty, and the exit is 26 on Windows (BL-359 Notes)
    /// and 65 elsewhere (BL-402 Notes).
    /// </summary>
    private TransferResult BodyRewindFailure(string target, RedirectChain chain, TransferResult result)
    {
        chain.Followed(target);
        chain.Refused(keepsRedirectUrl: false);
        return rewindFailsAsReadError
            ? TransferResult.Failure(CurlExitCode.ReadError, "read error getting mime data", result.BytesTransferred)
            : TransferResult.Failure(CurlExitCode.SendFailRewind, CannotRewindMessage, result.BytesTransferred);
    }

    /// <summary>
    /// Whether the <c>-F</c> body is kept for the next hop but its stream, holding a file part
    /// read from a pipe or device, cannot seek back to its start.
    /// </summary>
    private static bool CannotBeResent(Stream? bodyContent, bool bodyDropped) =>
        !bodyDropped && bodyContent is { CanSeek: false };

    private bool TrySelectHopProxy(
        ITransferContext first,
        HttpRequestOptions http,
        CurlUrl url,
        out HopProxy hopProxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        if (selectHopProxy is null)
        {
            hopProxy = new HopProxy(first.Proxy, http.ForwardProxy);
            failure = null;
            return true;
        }

        bool selected = selectHopProxy(url, out ProxyEndpoint? proxy, out failure);
        hopProxy = new HopProxy(proxy, proxy);
        return selected;
    }

    private static string? RedirectTarget(TransferResult result) =>
        result.IsSuccess && result.Report is { ResponseCode: >= 300 and < 400, RedirectUrl: { } url }
            ? url
            : null;

    /// <summary>
    /// Why <paramref name="target" /> is not followed, or <see langword="null" /> when it is.
    /// Only the limit refusal keeps <see cref="TransferReport.RedirectUrl" />: curl 8.21.0 writes
    /// an empty <c>%{redirect_url}</c> after refusing a target that does not parse or whose
    /// scheme it refuses (measured, BL-289). Every other refusal counts the redirect in
    /// <c>%{num_redirects}</c>, and only the <c>--proto-redir</c> or <c>--proto</c> refusal
    /// (<c>ReachesTarget</c>) moves <c>%{url_effective}</c> to the target (measured, BL-1277). A target that parses is switched to <c>https</c>
    /// (<see cref="SwitchedToHttps" />) before its scheme is checked, as curl switches it before
    /// looking the scheme up.
    /// </summary>
    private (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl, bool ReachesTarget)? Refusal(
        ref string target,
        string? methodSwitchMessage,
        ITransferContext first,
        int followed,
        RedirectPolicy policy,
        out CurlUrl? next)
    {
        next = null;
        if (policy.MaxRedirects >= 0 && followed >= policy.MaxRedirects)
        {
            return (CurlExitCode.TooManyRedirects, $"Maximum ({policy.MaxRedirects}) redirects followed", true, false);
        }

        if (ParseRefusal(target, first.PathAsIs, out next) is { } parseRefusal)
        {
            return parseRefusal;
        }

        first.Events.ReportInfo(IssueAnotherRequestMessagePrefix + target + "'");
        if (methodSwitchMessage is not null)
        {
            first.Events.ReportInfo(methodSwitchMessage);
        }

        next = SwitchedToHttps(ref target, next!, first);
        return ProtocolDisabledRefusal(next.Scheme, policy, first);
    }

    /// <summary>
    /// curl's refusal of a <paramref name="target" /> that names a scheme it does not know
    /// (exit 1) or does not parse (exit 3), or <see langword="null" /> with the parsed URL in
    /// <paramref name="next" />. The scheme is checked first, as curl's <c>parseurl</c> does, so a
    /// target naming no scheme it knows is never parsed with a guessed one (CGR-018).
    /// </summary>
    private (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl, bool ReachesTarget)? ParseRefusal(
        string target,
        bool pathAsIs,
        out CurlUrl? next)
    {
        next = null;
        if (NamesSchemeCurlCannotParse(target))
        {
            return (CurlExitCode.UnsupportedProtocol, "The redirect target URL could not be parsed: Unsupported URL scheme", false, false);
        }

        if (HasControlByte(target))
        {
            return (CurlExitCode.UrlMalformat, $"The redirect target URL could not be parsed: {ProxyUrlParser.MalformedReason}", false, false);
        }

        return CurlUrl.TryParse(EncodedForFollow(target), pathAsIs, out next)
            ? null
            : (CurlExitCode.UrlMalformat, $"The redirect target URL could not be parsed: {UnparsableUrlReason(target)}", false, false);
    }

    /// <summary>
    /// The redirect target the HSTS cache switches to <c>https</c>, reported to the hop's events
    /// as curl's <c>-v</c> line, or <paramref name="url" /> unchanged.
    /// </summary>
    private CurlUrl SwitchedToHttps(ref string target, CurlUrl url, ITransferContext first)
    {
        if (hsts is null || !hsts.TrySwitchToHttps(target, url, out string? httpsUrl))
        {
            return url;
        }

        first.Events.ReportInfo(HstsTransferPolicy.SwitchedMessagePrefix + httpsUrl);
        target = httpsUrl;
        return CurlUrl.Parse(httpsUrl, first.PathAsIs);
    }

    /// <summary>
    /// The <c>--proto-redir</c> or <c>--proto</c> refusal of a target whose scheme is
    /// <paramref name="scheme" />, or <see langword="null" /> when it is allowed. A refusal is also
    /// reported to the hop's events, as curl 8.21.0's <c>-v</c> writes it before its
    /// <c>curl: (1)</c> line (measured, BL-805 Notes).
    /// </summary>
    private static (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl, bool ReachesTarget)? ProtocolDisabledRefusal(
        string scheme,
        RedirectPolicy policy,
        ITransferContext first)
    {
        if (policy.AllowedSchemes.Contains(scheme) && policy.AllowedTransferSchemes?.Contains(scheme) != false)
        {
            return null;
        }

        string message = $"Protocol \"{scheme}\" is disabled (in redirect)";
        first.Events.ReportInfo(message);
        return (CurlExitCode.UnsupportedProtocol, message, false, true);
    }

    /// <summary>
    /// The target as curl 8.21.0 follows it (CURLU_URLENCODE with CURLU_ALLOW_SPACE): a space
    /// becomes <c>+</c> in the query and <c>%20</c> elsewhere, and a raw byte at or above 0x80
    /// (one Latin-1 character of the header) becomes its own percent escape (BL-1801). An
    /// authority that is text rather than header bytes - the request URL's own, which a relative
    /// <c>Location</c> keeps, such as the IDN host <c>åäö.se</c> - is kept as it is, so its host
    /// stays that name (upstream tests 2046 and 2047, BL-2961).
    /// </summary>
    internal static string EncodedForFollow(string target)
    {
        System.Text.StringBuilder encoded = new(target.Length);
        int hash = target.IndexOf('#', StringComparison.Ordinal);
        int question = target.IndexOf('?', StringComparison.Ordinal);
        int queryStart = question < 0 ? target.Length : question + 1;
        int queryEnd = hash < 0 ? target.Length : hash;
        (int textStart, int textEnd) = TextAuthority(target);
        for (int index = 0; index < target.Length; index++)
        {
            encoded.Append(IsWithin(index, textStart, textEnd)
                ? target[index].ToString()
                : EncodedCharacter(target[index], IsWithin(index, queryStart, queryEnd)));
        }

        return encoded.ToString();
    }

    private static bool IsWithin(int index, int start, int end) => index >= start && index < end;

    /// <summary>
    /// The start and end of <paramref name="target" />'s authority when it is text: when it holds
    /// a character above <c>U+00FF</c>, or Latin-1 characters whose bytes are not valid UTF-8, so
    /// they cannot be the raw bytes of a header. An empty range otherwise.
    /// </summary>
    private static (int Start, int End) TextAuthority(string target)
    {
        int separator = target.IndexOf("://", StringComparison.Ordinal);
        if (separator < 0)
        {
            return (0, 0);
        }

        int start = separator + 3;
        ReadOnlySpan<char> rest = target.AsSpan(start);
        ReadOnlySpan<char> authority = rest[..IndexOrLength(rest, rest.IndexOfAny('/', '?', '#'))];
        return IsHeaderBytes(authority) ? (0, 0) : (start, start + authority.Length);
    }

    private static bool IsHeaderBytes(ReadOnlySpan<char> authority) =>
        !authority.ContainsAnyExceptInRange('\0', 'ÿ') && System.Text.Unicode.Utf8.IsValid(System.Text.Encoding.Latin1.GetBytes(authority.ToArray()));

    private static string EncodedCharacter(char character, bool inQuery) => character switch
    {
        ' ' => inQuery ? "+" : "%20",
        > '~' and <= 'ÿ' => "%" + ((int)character).ToString("X2", CultureInfo.InvariantCulture),
        _ => character.ToString(),
    };

    /// <summary>
    /// Whether <paramref name="target" /> starts with an RFC 3986 scheme and a colon, with or
    /// without <c>//</c>, that curl does not know: curl 8.22.0 reads <c>localhost:20200/x</c> as
    /// the scheme <c>localhost</c> and refuses it, where a URL typed on the command line would
    /// have its scheme guessed (CGR-018).
    /// </summary>
    private bool NamesSchemeCurlCannotParse(string target)
    {
        int colon = target.IndexOf(':', StringComparison.Ordinal);
        bool namesScheme = colon > (driveLetterIsNotScheme ? 1 : 0)
            && char.IsAsciiLetter(target[0])
            && !target.AsSpan(1, colon - 1).ContainsAnyExcept(SchemeCharacters);
        return namesScheme && !SchemesCurlParses.Contains(target[..colon].ToLowerInvariant());
    }

    private static string UnparsableUrlReason(string target)
    {
        ReadOnlySpan<char> authority = target.AsSpan(target.IndexOf("//", StringComparison.Ordinal) + 2);
        authority = authority[..IndexOrLength(authority, authority.IndexOfAny('/', '?', '#'))];
        authority = authority[(authority.LastIndexOf('@') + 1)..];
        if (authority.StartsWith("["))
        {
            return ProxyUrlParser.BadIPv6Reason;
        }

        int colon = authority.IndexOf(':');
        return colon >= 0 && !IsPort(authority[(colon + 1)..])
            ? ProxyUrlParser.BadPortReason
            : ProxyUrlParser.BadHostnameReason;
    }

    /// <summary>
    /// Whether <paramref name="target" /> holds a control byte (0x00-0x1F) or DEL, which curl 8.22.0's
    /// URL parser refuses as malformed input: the HTTP handler escapes them in a relative
    /// <c>Location</c>, so only an absolute one still holds them (CGR-061).
    /// </summary>
    private static bool HasControlByte(string target) =>
        target.AsSpan().ContainsAnyInRange('\0', '\x1f') || target.Contains('\x7f', StringComparison.Ordinal);

    private static int IndexOrLength(ReadOnlySpan<char> text, int index) => index < 0 ? text.Length : index;

    private static bool IsPort(ReadOnlySpan<char> text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port <= 65535;

    private static bool DropsBody(int responseCode, ITransferContext hop, RedirectPolicy policy)
    {
        bool posts = hop.Http!.Body is not null;
        return responseCode switch
        {
            301 => posts && !policy.KeepPostOn301,
            302 => posts && !policy.KeepPostOn302,
            303 => posts ? !policy.KeepPostOn303 : hop.Upload is not null,
            _ => false,
        };
    }

    /// <summary>
    /// Whether <c>--follow</c> drops the <c>-X</c> method for the next hop: when the request switches
    /// to GET, as it does once the body is dropped and on a 303 to a request without a body, as curl
    /// 8.21.0 sends <c>GET</c> after <c>--follow -X DELETE</c> meets a 303 but <c>DELETE</c> after a
    /// 301 or 302 (measured, BL-627 Notes).
    /// </summary>
    private static bool DropsCustomMethod(int responseCode, ITransferContext hop, RedirectPolicy policy, bool bodyDropped) =>
        policy.DropsCustomMethodOnSwitchToGet && (bodyDropped || (responseCode == 303 && hop.Http!.Body is null));

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0's <c>http_switch_to_get</c> writes after
    /// <c>Issue another request to this URL</c>, or <see langword="null" />: under <c>--follow</c>,
    /// <c>Switch to GET because of N response</c> when a request that is not a plain GET switches;
    /// under <c>-L</c>, <c>Stick to M instead of GET</c> when a <c>-X</c> method is kept. curl calls
    /// it for a POST on a 301 or 302 without <c>--post301</c> or <c>--post302</c>, and for every
    /// 303 except a POST under <c>--post303</c> (lib/http.c; measured 2026-10-03, BL-1352).
    /// </summary>
    private static string? MethodSwitchMessage(int responseCode, ITransferContext hop, string? customMethod, RedirectPolicy policy)
    {
        if (!SwitchesToGet(responseCode, hop, policy))
        {
            return null;
        }

        if (policy.DropsCustomMethodOnSwitchToGet && !IsPlainGet(hop, customMethod))
        {
            return $"Switch to GET because of {responseCode} response";
        }

        return customMethod is null || policy.SendsCustomMethodFirstOnly ? null : $"Stick to {customMethod} instead of GET";
    }

    private static bool IsPlainGet(ITransferContext hop, string? customMethod) =>
        customMethod is null && hop.Http!.Body is null && hop.Upload is null;

    private static bool SwitchesToGet(int responseCode, ITransferContext hop, RedirectPolicy policy)
    {
        bool posts = hop.Http!.Body is not null;
        return responseCode switch
        {
            301 => posts && !policy.KeepPostOn301,
            302 => posts && !policy.KeepPostOn302,
            303 => !(posts && policy.KeepPostOn303),
            _ => false,
        };
    }

    private static HttpRequestOptions HopMethod(HttpRequestOptions http, bool methodDropped, RedirectPolicy policy) =>
        methodDropped || policy.SendsCustomMethodFirstOnly ? http with { CustomMethod = null } : http;

    private static long? SeekableStart(Stream? upload) =>
        upload is { CanSeek: true } ? upload.Position : null;

    private static Stream? StreamBodyContent(HttpRequestOptions http) => (http.Body as StreamBody)?.Content;

    private static void Rewind(Stream? content, long? start, bool bodyDropped)
    {
        if (!bodyDropped && start is { } position)
        {
            // A seekable stream's start was only recorded when the stream exists.
            content!.Position = position;
        }
    }

    /// <summary>
    /// Seeks a kept seekable upload back to where it started; gives the <see cref="IOException" />'s
    /// message when the stream cannot go back (a sized read-callback upload with no seek callback,
    /// upstream test3034, BL-2703), <see langword="null" /> when it went back or none was needed.
    /// </summary>
    private static string? SeekUploadToStart(Stream? upload, long? start, bool bodyDropped)
    {
        try
        {
            Rewind(upload, start, bodyDropped);
            return null;
        }
        catch (IOException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Rewinds a kept upload that cannot seek but implements <see cref="IUploadRewinder" />, once
    /// some of it was read, as curl 8.22.0's <c>cr_in_rewind</c> does when the next hop starts; gives
    /// the hop's failure when the rewind fails, or when <paramref name="seekFailure" /> says a seekable
    /// upload could not go back: exit 65 with that message, after the <c>-v</c> lines for it and
    /// <see cref="IUploadRewinder.ReaderRewindFailedLine" />.
    /// <see langword="null" /> when the rewind succeeded or none was needed.
    /// </summary>
    private static TransferResult? UploadRewindFailure(ITransferContext context, string? seekFailure, long? uploadStart, bool bodyDropped, TransferResult result)
    {
        if ((seekFailure ?? RewinderFailure(context, uploadStart, bodyDropped)) is not { } message)
        {
            return null;
        }

        context.Events.ReportInfo(message);
        context.Events.ReportInfo(IUploadRewinder.ReaderRewindFailedLine);
        return TransferResult.Failure(CurlExitCode.SendFailRewind, message, result.BytesTransferred);
    }

    /// <summary>
    /// The message of a failed rewind through <see cref="IUploadRewinder" /> of a kept upload that
    /// cannot seek and was read; <see langword="null" /> when it rewound or none was needed.
    /// </summary>
    private static string? RewinderFailure(ITransferContext context, long? uploadStart, bool bodyDropped) =>
        bodyDropped || uploadStart is not null || context.Upload is not IUploadRewinder { HasBeenRead: true } rewinder
            ? null
            : rewinder.RewindToStart();

    internal static bool IsSameOrigin(CurlUrl first, CurlUrl next) =>
        string.Equals(first.Scheme, next.Scheme, StringComparison.Ordinal)
        && string.Equals(first.Host, next.Host, StringComparison.OrdinalIgnoreCase)
        && first.Port == next.Port;

    /// <summary>
    /// The hop's HTTP options with its <c>--alt-svc</c> route: the selector's lookup for
    /// <paramref name="url" />, or, without one, the first hop's route for a hop to the first
    /// URL's origin and none for any other.
    /// </summary>
    private static HttpRequestOptions HopAltSvc(CurlUrl first, CurlUrl url, HttpRequestOptions http, HopAltSvcSelector? selectHopAltSvc) =>
        selectHopAltSvc is null
            ? http with { AltSvcRoute = IsSameOrigin(first, url) ? http.AltSvcRoute : null }
            : selectHopAltSvc(url, http);

    private static HttpRequestOptions HopHttp(CurlUrl previousUrl, HttpRequestOptions http, ProxyEndpoint? forwardProxy, bool bodyDropped, bool sendCredentials)
    {
        HttpRequestOptions hopHttp = http with
        {
            Body = bodyDropped ? null : http.Body,
            ForwardProxy = forwardProxy,
            Referer = http.AutoReferer ? AutoReferer(previousUrl) : http.Referer,
        };
        return sendCredentials
            ? hopHttp
            : hopHttp with
            {
                BearerToken = null,
                HttpSignature = null,
                KeptDigestAuthorization = null,
                Headers = [.. hopHttp.Headers.Where(header => !IsCredentialHeader(header))],
                SendsCookieStrings = false,
            };
    }

    /// <summary>
    /// The <c>Referer</c> <c>-e "...;auto"</c> sends on a followed hop: the URL the redirect came
    /// from without user information or fragment, its query kept, as curl 8.21.0 sends
    /// <c>http://127.0.0.1:18361/a?q=1</c> after <c>http://u:p@127.0.0.1:18361/a?q=1#f</c>
    /// (measured, BL-361 Notes).
    /// </summary>
    private static string AutoReferer(CurlUrl url) =>
        url.Query is null
            ? $"{url.Scheme}://{HostAndPort(url)}{url.AbsolutePath}"
            : $"{url.Scheme}://{HostAndPort(url)}{url.AbsolutePath}?{url.Query}";

    private static string HostAndPort(CurlUrl url) =>
        url.IsDefaultPort ? url.Host : string.Create(CultureInfo.InvariantCulture, $"{url.Host}:{url.Port}");

    /// <summary>
    /// Tells whether an <c>-H</c> value names <c>Authorization</c> or <c>Cookie</c>, with any
    /// blanks between the name and its colon ignored, as curl 8.22.0 drops
    /// <c>Authorization : s3cr3t</c> from a redirect to another host (upstream test2113, BL-2163).
    /// </summary>
    private static bool IsCredentialHeader(string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        ReadOnlySpan<char> name = colon < 0 ? [] : header.AsSpan(0, colon).TrimEnd(" \t");
        return name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The mail options a hop carries: the first hop's, with the <c>--oauth2-bearer</c> token
    /// withheld from a hop that may not be sent credentials, as curl 8.21.0 sends no
    /// <c>AUTHENTICATE</c> and no <c>LOGIN</c> after an HTTP redirect to IMAP on another host
    /// (upstream test779, BL-2090).
    /// </summary>
    private static MailRequestOptions? HopMail(ITransferContext first, bool sendCredentials)
    {
        MailRequestOptions? mail = first.Mail;
        if (sendCredentials)
        {
            return mail;
        }

        if (mail is null)
        {
            // An HTTP first URL carries no mail options, but its --oauth2-bearer token is withheld all the same.
            return first.Http!.BearerToken is null ? null : new MailRequestOptions { BearerTokenWithheld = true };
        }

        return mail.BearerToken is null ? mail : mail with { BearerToken = null, BearerTokenWithheld = true };
    }

    private static TransferContext NextHop(
        ITransferContext first,
        CurlUrl previousUrl,
        CurlUrl url,
        HttpRequestOptions http,
        HopProxy hopProxy,
        bool bodyDropped,
        bool sendCredentials,
        long operationStarted,
        HopAltSvcSelector? selectHopAltSvc,
        HopCredentialSelector? selectHopCredentials) =>
        new()
        {
            Url = url,
            Output = first.Output,
            Upload = bodyDropped ? null : first.Upload,
            ResumeFrom = first.ResumeFrom,
            ResumeUploadFromUnknownOffset = !bodyDropped && first.ResumeUploadFromUnknownOffset,
            Range = first.Range,
            RangeText = first.RangeText,
            MaxFileSize = first.MaxFileSize,
            NoBody = first.NoBody,
            TimeCondition = first.TimeCondition,
            HeaderOutput = first.HeaderOutput,
            PostData = bodyDropped ? null : first.PostData,
            Credentials = selectHopCredentials is null ? HopCredentials(first, url, sendCredentials) : selectHopCredentials(url),
            Mail = HopMail(first, sendCredentials),
            TelnetOptions = first.TelnetOptions,
            TftpBlockSize = first.TftpBlockSize,
            TftpNoOptions = first.TftpNoOptions,
            ConvertLineEndings = first.ConvertLineEndings,
            CreateFileMode = first.CreateFileMode,
            PathAsIs = first.PathAsIs,
            ConnectTimeout = first.ConnectTimeout,
            MaxTime = first.MaxTime,
            OperationStarted = operationStarted,
            Proxy = hopProxy.Proxy,
            Http = HopAltSvc(first.Url, url, HopHttp(previousUrl, http, hopProxy.ForwardProxy, bodyDropped, sendCredentials), selectHopAltSvc) with
            {
                FollowedToAnotherHost = !string.Equals(first.Url.Host, url.Host, StringComparison.OrdinalIgnoreCase),
            },
            TimeProvider = first.TimeProvider,
            CancellationToken = first.CancellationToken,
            Progress = first.Progress,
            Events = first.Events,
            DiagnosticLog = first.DiagnosticLog,
        };

    /// <summary>
    /// The credentials a hop sends: the first hop's command-line (<c>-u</c> or netrc)
    /// credentials when <paramref name="sendCommandLineCredentials" />, else those written in the
    /// hop's own URL, as curl 8.21.0 sends <c>c:d</c> after a 302 to <c>http://c:d@localhost/b</c>
    /// from <c>-u q:r</c>, and <c>q:r</c> after one to <c>http://c:d@</c> the same host (BL-814 Notes).
    /// </summary>
    private static NetworkCredential? HopCredentials(ITransferContext first, CurlUrl url, bool sendCommandLineCredentials) =>
        sendCommandLineCredentials && CommandLineCredentials(first) is { } commandLine
            ? commandLine
            : CredentialsWrittenIn(url);

    /// <summary>
    /// The first hop's credentials when they did not come from its URL's user information:
    /// credentials equal to the URL's own are taken as the URL's.
    /// </summary>
    private static NetworkCredential? CommandLineCredentials(ITransferContext first) =>
        first.Credentials is { } credentials && !AreEqual(credentials, CredentialsWrittenIn(first.Url))
            ? credentials
            : null;

    private static bool AreEqual(NetworkCredential credentials, NetworkCredential? other) =>
        other is not null
        && string.Equals(credentials.UserName, other.UserName, StringComparison.Ordinal)
        && string.Equals(credentials.Password, other.Password, StringComparison.Ordinal);

    /// <summary>
    /// The percent-decoded user name and password written in <paramref name="url" />, either one
    /// empty when absent, or <see langword="null" /> when it gives neither, as curl 8.21.0 sends
    /// <c>c:</c> for <c>http://c@host/</c> and nothing for <c>http://@host/</c>.
    /// </summary>
    private static NetworkCredential? CredentialsWrittenIn(CurlUrl url)
    {
        string? user = DecodedUserInformation(url.User);
        string? password = DecodedUserInformation(url.Password);
        return user is null && password is null ? null : new NetworkCredential(user ?? string.Empty, password ?? string.Empty);
    }

    private static string? DecodedUserInformation(string? encoded) =>
        string.IsNullOrEmpty(encoded) ? null : Uri.UnescapeDataString(encoded);

    /// <summary>
    /// The proxy one hop connects through (<see cref="ITransferContext.Proxy" />) and the one its
    /// HTTP request is forwarded to (<see cref="HttpRequestOptions.ForwardProxy" />).
    /// </summary>
    private readonly record struct HopProxy(ProxyEndpoint? Proxy, ProxyEndpoint? ForwardProxy);

    /// <summary>
    /// What the hops of one chain add up to, merged into the last hop's report.
    /// </summary>
    private sealed class RedirectChain(TimeProvider timeProvider)
    {
        private readonly RedirectHopTimings timings = new(timeProvider);
        private TransferReport? lastReport;
        private string? lastReferer;
        private string? effectiveUrl;
        private long headerSize;
        private long requestSize;
        private int connectionCount;
        private bool redirectUrlCleared;
        private bool targetHoldsHeaderBytes;
        private readonly List<KeyValuePair<string, string>> earlierResponseHeaders = [];

        public int RedirectCount { get; private set; }

        public void Add(TransferReport? report, string? referer)
        {
            earlierResponseHeaders.AddRange(lastReport?.ResponseHeaders ?? []);
            lastReport = report;
            lastReferer = referer;
            if (report is null)
            {
                return;
            }

            timings.Add(report.Timings);
            headerSize += report.HeaderSize;
            requestSize += report.RequestSize;
            connectionCount += report.ConnectionCount;
            RedirectCount += report.RedirectCount;
        }

        /// <summary>
        /// Takes the last hop's <see cref="TransferReport.RedirectUrl" /> as the next target: the
        /// target with its header bytes unmarked (<see cref="HeaderByteText" />), for parsing and
        /// following. A target that held marked bytes came whole from an absolute <c>Location</c>,
        /// so the effective URL it becomes is marked again, and <c>%{url_effective}</c> writes
        /// those bytes back as curl 8.22.0 does (CGR-062, ADR-0824).
        /// </summary>
        public string Targeted(string redirectUrl)
        {
            targetHoldsHeaderBytes = HeaderByteText.HasMarks(redirectUrl);
            return HeaderByteText.Unmark(redirectUrl);
        }

        public void Followed(string url)
        {
            effectiveUrl = AsEffectiveUrl(url);
            RedirectCount++;
        }

        private string AsEffectiveUrl(string url) => targetHoldsHeaderBytes ? HeaderByteText.Mark(url) : url;

        /// <summary>
        /// Records that the last hop's redirect target was refused, clearing its
        /// <see cref="TransferReport.RedirectUrl" /> unless <paramref name="keepsRedirectUrl" />.
        /// </summary>
        public void Refused(bool keepsRedirectUrl) => redirectUrlCleared = !keepsRedirectUrl;

        /// <summary>
        /// Counts a redirect whose target was refused, as curl does for every refusal but the
        /// <c>--max-redirs</c> limit, moving the effective URL to <paramref name="reachedUrl" />
        /// unless it is <see langword="null" />.
        /// </summary>
        public void CountRefused(string? reachedUrl)
        {
            effectiveUrl = reachedUrl is null ? effectiveUrl : AsEffectiveUrl(reachedUrl);
            RedirectCount++;
        }

        public TransferResult Merge(TransferResult outcome)
        {
            TransferReport report = lastReport ?? new TransferReport();
            return outcome with
            {
                Report = report with
                {
                    EffectiveUrl = effectiveUrl,
                    RedirectUrl = redirectUrlCleared ? null : report.RedirectUrl,
                    RedirectCount = RedirectCount,
                    Referer = lastReferer,
                    HeaderSize = headerSize,
                    RequestSize = requestSize,
                    ConnectionCount = connectionCount,
                    EarlierResponseHeaders = [.. earlierResponseHeaders],
                    Timings = timings.Merge(report.Timings),
                },
            };
        }
    }
}
