using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// <see cref="RedirectPolicy.AllowedSchemes" /> does not allow with exit 1,
/// <c>Protocol "file" is disabled (in redirect)</c>.
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
/// Every hop after the first goes through the proxy <see cref="HopProxySelector" /> chooses for
/// that hop's own URL, set as both <see cref="ITransferContext.Proxy" /> and
/// <see cref="HttpRequestOptions.ForwardProxy" />, as curl 8.21.0 chooses again for each hop: a
/// redirect to a <c>--noproxy</c> host, or from <c>http</c> to <c>https</c> with only
/// <c>http_proxy</c> set, goes direct (measured, BL-329 Notes). A selector failure ends the
/// chain with that failure. Without a selector, every hop keeps the first URL's proxy.
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
public sealed class RedirectFollower(ProtocolDispatcher dispatcher, HopProxySelector? selectHopProxy = null, bool? runsOnWindows = null)
{
    /// <summary>The Linux and macOS build's message for a multipart body it cannot rewind for the next hop.</summary>
    public const string CannotRewindMessage = "Cannot rewind mime/post data";

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
            : dispatcher.DispatchAsync(context);
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
            TransferResult result = await dispatcher.DispatchAsync(hop);
            chain.Add(result.Report);
            if (RedirectTarget(result) is not { } target)
            {
                return chain.Merge(result);
            }

            bodyDropped |= DropsBody(result.Report!.ResponseCode, hop, policy);
            bool bodyCannotBeResent = CannotBeResent(bodyContent, bodyDropped);
            if (StopBeforeHop(context, http, target, chain, policy, result, bodyCannotBeResent, out CurlUrl? next, out HopProxy hopProxy) is { } stop)
            {
                return chain.Merge(stop);
            }

            Rewind(context.Upload, uploadStart, bodyDropped);
            Rewind(bodyContent, bodyStart, bodyDropped);
            // No stop means the target parsed, so next is set.
            hop = NextHop(context, next!, http, hopProxy, bodyDropped, policy.LocationTrusted || IsSameOrigin(context.Url, next!), operationStarted);
            chain.Followed(target);
        }
    }

    /// <summary>
    /// The failure that ends the chain instead of following <paramref name="target" />: a
    /// <see cref="Refusal" />, the hop proxy selector's failure, or - when
    /// <paramref name="bodyCannotBeResent" /> - curl's failure for a multipart body it cannot
    /// rewind, which counts the redirect as followed; <see langword="null" />, with
    /// <paramref name="next" /> and <paramref name="hopProxy" /> set, when the hop goes ahead.
    /// </summary>
    private TransferResult? StopBeforeHop(
        ITransferContext first,
        HttpRequestOptions http,
        string target,
        RedirectChain chain,
        RedirectPolicy policy,
        TransferResult result,
        bool bodyCannotBeResent,
        out CurlUrl? next,
        out HopProxy hopProxy)
    {
        hopProxy = default;
        if (Refusal(target, first.PathAsIs, chain.RedirectCount, policy, out next) is { } refusal)
        {
            chain.Refused(refusal.KeepsRedirectUrl);
            return TransferResult.Failure(refusal.ExitCode, refusal.Message, result.BytesTransferred);
        }

        // No refusal means the target parsed, so next is set.
        if (!TrySelectHopProxy(first, http, next!, out hopProxy, out TransferResult? failure))
        {
            return failure;
        }

        return bodyCannotBeResent ? BodyRewindFailure(target, chain, result) : null;
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
    /// scheme it refuses (measured, BL-289).
    /// </summary>
    private static (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl)? Refusal(
        string target,
        bool pathAsIs,
        int followed,
        RedirectPolicy policy,
        out CurlUrl? next)
    {
        next = null;
        if (policy.MaxRedirects >= 0 && followed >= policy.MaxRedirects)
        {
            return (CurlExitCode.TooManyRedirects, $"Maximum ({policy.MaxRedirects}) redirects followed", true);
        }

        if (!CurlUrl.TryParse(target, pathAsIs, out next))
        {
            return (CurlExitCode.UrlMalformat, $"The redirect target URL could not be parsed: {UnparsableUrlReason(target)}", false);
        }

        return SchemeRefusal(next.Scheme, policy);
    }

    private static (CurlExitCode ExitCode, string Message, bool KeepsRedirectUrl)? SchemeRefusal(string scheme, RedirectPolicy policy)
    {
        if (!SchemesCurlParses.Contains(scheme))
        {
            return (CurlExitCode.UnsupportedProtocol, "The redirect target URL could not be parsed: Unsupported URL scheme", false);
        }

        return policy.AllowedSchemes.Contains(scheme)
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

    private static HttpRequestOptions HopHttp(HttpRequestOptions http, ProxyEndpoint? forwardProxy, bool bodyDropped, bool sendCredentials)
    {
        HttpRequestOptions hopHttp = http with { Body = bodyDropped ? null : http.Body, ForwardProxy = forwardProxy };
        return sendCredentials
            ? hopHttp
            : hopHttp with
            {
                BearerToken = null,
                Headers = [.. hopHttp.Headers.Where(header => !IsCredentialHeader(header))],
            };
    }

    private static bool IsCredentialHeader(string header) =>
        header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)
        || header.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase);

    private static TransferContext NextHop(
        ITransferContext first,
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
            MaxFileSize = first.MaxFileSize,
            NoBody = first.NoBody,
            TimeCondition = first.TimeCondition,
            HeaderOutput = first.HeaderOutput,
            PostData = bodyDropped ? null : first.PostData,
            Credentials = sendCredentials ? first.Credentials : null,
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
            Http = HopHttp(http, hopProxy.ForwardProxy, bodyDropped, sendCredentials),
            TimeProvider = first.TimeProvider,
            CancellationToken = first.CancellationToken,
            Progress = first.Progress,
            Events = first.Events,
        };

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
        private long? firstStarted;
        private string? effectiveUrl;
        private long headerSize;
        private long requestSize;
        private int connectionCount;
        private bool redirectUrlCleared;

        public int RedirectCount { get; private set; }

        public void Add(TransferReport? report)
        {
            lastReport = report;
            if (report is null)
            {
                return;
            }

            firstStarted ??= report.Timings?.Started;
            headerSize += report.HeaderSize;
            requestSize += report.RequestSize;
            connectionCount += report.ConnectionCount;
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
