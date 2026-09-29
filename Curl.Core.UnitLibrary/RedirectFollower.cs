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
/// Bad IPv6 address</c>, <c>Bad hostname</c> or the port reason), then the target's scheme: one this curl build cannot parse fails with exit 1,
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
/// The run's HSTS cache, which learns from every hop's response and switches every <c>http</c>
/// redirect target it knows to <c>https</c> before the target's scheme is checked, reporting
/// <see cref="HstsTransferPolicy.SwitchedMessagePrefix" /> and the URL to the hop's events, as
/// curl 8.21.0 does after <c>Issue another request to this URL</c> (BL-621 Notes);
/// <see langword="null" /> for none.
/// </param>
public sealed class RedirectFollower(
    ProtocolDispatcher dispatcher,
    HopProxySelector? selectHopProxy = null,
    bool? runsOnWindows = null,
    HstsTransferPolicy? hsts = null)
{
    /// <summary>The Linux and macOS build's message for a multipart body it cannot rewind for the next hop.</summary>
    public const string CannotRewindMessage = "Cannot rewind mime/post data";

    /// <summary>
    /// curl 8.21.0's message, with exit 67, for a URL with user information under
    /// <c>--disallow-username-in-url</c> (BL-626 Notes).
    /// </summary>
    public const string CredentialsInUrlMessage = "URL rejected: Credentials was passed in the URL when prohibited";

    private readonly bool rewindFailsAsReadError = runsOnWindows ?? OperatingSystem.IsWindows();

    private static readonly HashSet<string> SchemesCurlParses = new(
        [
            "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap",
            "imaps", "ldap", "ldaps", "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp",
            "smtp", "smtps", "telnet", "tftp", "ws", "wss",
        ],
        StringComparer.Ordinal);

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
    /// <see cref="TransferReport.Timings" /> measured from the first hop's start with
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

    /// <summary>Performs one hop and teaches the HSTS cache from its response.</summary>
    private async ValueTask<TransferResult> DispatchAsync(ITransferContext hop, RedirectPolicy policy)
    {
        TransferResult result = await dispatcher.DispatchAsync(hop, policy.AllowedTransferSchemes);
        hsts?.LearnFrom(hop.Url, result.Report);
        return result;
    }

    private async ValueTask<TransferResult> FollowChainAsync(
        ITransferContext context,
        HttpRequestOptions http,
        RedirectPolicy policy)
    {
        RedirectChain chain = new(context.TimeProvider);
        long operationStarted = context.OperationStarted ?? context.TimeProvider.GetTimestamp();
        long? uploadStart = SeekableStart(context.Upload);
        Stream? bodyContent = StreamBodyContent(http);
        long? bodyStart = SeekableStart(bodyContent);
        ITransferContext hop = context;
        bool bodyDropped = false;
        while (true)
        {
            TransferResult result = await DispatchAsync(hop, policy);
            chain.Add(result.Report, hop.Http!.Referer);
            if (RedirectTarget(result) is not { } target)
            {
                return chain.Merge(result);
            }

            bodyDropped |= DropsBody(result.Report!.ResponseCode, hop, policy);
            bool bodyCannotBeResent = CannotBeResent(bodyContent, bodyDropped);
            if (StopBeforeHop(context, http, ref target, chain, policy, result, bodyCannotBeResent, out CurlUrl? next, out HopProxy hopProxy) is { } stop)
            {
                return chain.Merge(stop);
            }

            Rewind(context.Upload, uploadStart, bodyDropped);
            Rewind(bodyContent, bodyStart, bodyDropped);
            chain.Followed(target);
            // No stop means the target parsed, so next is set.
            hop = NextHop(context, hop.Url, next!, http with { RedirectsFollowed = chain.RedirectCount }, hopProxy, bodyDropped, policy.LocationTrusted || IsSameOrigin(context.Url, next!), operationStarted);
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
        RedirectChain chain,
        RedirectPolicy policy,
        TransferResult result,
        bool bodyCannotBeResent,
        out CurlUrl? next,
        out HopProxy hopProxy)
    {
        hopProxy = default;
        if (Refusal(ref target, first, chain.RedirectCount, policy, out next) is { } refusal)
        {
            chain.Refused(refusal.KeepsRedirectUrl);
            return TransferResult.Failure(refusal.ExitCode, refusal.Message, result.BytesTransferred);
        }

        // No refusal means the target parsed, so next is set.
        if (policy.DisallowsUserInUrl && next!.User is not null)
        {
            return CredentialsInUrlFailure(target, chain, result);
        }

        if (!TrySelectHopProxy(first, http, next!, out hopProxy, out TransferResult? failure))
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
    /// scheme it refuses (measured, BL-289). A target that parses is switched to <c>https</c>
    /// (<see cref="SwitchedToHttps" />) before its scheme is checked, as curl switches it before
    /// looking the scheme up.
    /// </summary>
    private (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl)? Refusal(
        ref string target,
        ITransferContext first,
        int followed,
        RedirectPolicy policy,
        out CurlUrl? next)
    {
        next = null;
        if (policy.MaxRedirects >= 0 && followed >= policy.MaxRedirects)
        {
            return (CurlExitCode.TooManyRedirects, $"Maximum ({policy.MaxRedirects}) redirects followed", true);
        }

        if (!CurlUrl.TryParse(target, first.PathAsIs, out next))
        {
            return (CurlExitCode.UrlMalformat, $"The redirect target URL could not be parsed: {UnparsableUrlReason(target)}", false);
        }

        next = SwitchedToHttps(ref target, next, first);
        return SchemeRefusal(next.Scheme, policy);
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

    private static (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl)? SchemeRefusal(string scheme, RedirectPolicy policy)
    {
        if (!SchemesCurlParses.Contains(scheme))
        {
            return (CurlExitCode.UnsupportedProtocol, "The redirect target URL could not be parsed: Unsupported URL scheme", false);
        }

        return policy.AllowedSchemes.Contains(scheme) && policy.AllowedTransferSchemes?.Contains(scheme) != false
            ? null
            : (CurlExitCode.UnsupportedProtocol, $"Protocol \"{scheme}\" is disabled (in redirect)", false);
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

    private static bool IsSameOrigin(CurlUrl first, CurlUrl next) =>
        string.Equals(first.Scheme, next.Scheme, StringComparison.Ordinal)
        && string.Equals(first.Host, next.Host, StringComparison.OrdinalIgnoreCase)
        && first.Port == next.Port;

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
                Headers = [.. hopHttp.Headers.Where(header => !IsCredentialHeader(header))],
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

    private static bool IsCredentialHeader(string header) =>
        header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)
        || header.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase);

    private static TransferContext NextHop(
        ITransferContext first,
        CurlUrl previousUrl,
        CurlUrl url,
        HttpRequestOptions http,
        HopProxy hopProxy,
        bool bodyDropped,
        bool sendCredentials,
        long operationStarted) =>
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
            Credentials = HopCredentials(first, url, sendCredentials),
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
            Http = HopHttp(previousUrl, http, hopProxy.ForwardProxy, bodyDropped, sendCredentials) with { AltSvcRoute = IsSameOrigin(first.Url, url) ? http.AltSvcRoute : null },
            TimeProvider = first.TimeProvider,
            CancellationToken = first.CancellationToken,
            Progress = first.Progress,
            Events = first.Events,
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
        private TransferReport? lastReport;
        private string? lastReferer;
        private long? firstStarted;
        private string? effectiveUrl;
        private long headerSize;
        private long requestSize;
        private int connectionCount;
        private bool redirectUrlCleared;

        public int RedirectCount { get; private set; }

        public void Add(TransferReport? report, string? referer)
        {
            lastReport = report;
            lastReferer = referer;
            if (report is null)
            {
                return;
            }

            firstStarted ??= report.Timings?.Started;
            headerSize += report.HeaderSize;
            requestSize += report.RequestSize;
            connectionCount += report.ConnectionCount;
            RedirectCount += report.RedirectCount;
        }

        public void Followed(string url)
        {
            effectiveUrl = url;
            RedirectCount++;
        }

        /// <summary>
        /// Records that the last hop's redirect target was refused, clearing its
        /// <see cref="TransferReport.RedirectUrl" /> unless <paramref name="keepsRedirectUrl" />.
        /// </summary>
        public void Refused(bool keepsRedirectUrl) => redirectUrlCleared = !keepsRedirectUrl;

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
                    Timings = MergeTimings(report.Timings),
                },
            };
        }

        private TransferTimings? MergeTimings(TransferTimings? last)
        {
            if (last is null)
            {
                return null;
            }

            // The hop that reported these timings was added, so firstStarted is set.
            long started = firstStarted!.Value;
            return last with
            {
                Started = started,
                RedirectDuration = timeProvider.GetElapsedTime(started, last.Started),
            };
        }
    }
}
