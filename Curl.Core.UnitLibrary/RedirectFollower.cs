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
/// stays dropped for every later hop.
/// </para>
/// <para>
/// A hop whose host, port or scheme differs from the first URL's gets no
/// <see cref="ITransferContext.Credentials" />, no bearer token and no <c>-H</c>
/// <c>Authorization:</c> or <c>Cookie:</c> header, unless <c>--location-trusted</c>.
/// </para>
/// <para>
/// Every hop after the first carries the chain's start as
/// <see cref="ITransferContext.OperationStarted" />, so <c>-m</c> limits the whole chain, as
/// curl's does, rather than each hop (measured, BL-299 Notes).
/// </para>
/// </remarks>
/// <param name="dispatcher">The dispatcher that performs each hop.</param>
public sealed class RedirectFollower(ProtocolDispatcher dispatcher)
{
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

            if (Refusal(target, context.PathAsIs, chain.RedirectCount, policy, out CurlUrl? next) is { } refusal)
            {
                return chain.Merge(TransferResult.Failure(refusal.ExitCode, refusal.Message, result.BytesTransferred));
            }

            bodyDropped |= DropsBody(result.Report!.ResponseCode, hop, policy);
            RewindUpload(context.Upload, uploadStart, bodyDropped);
            // No refusal means the target parsed, so next is set.
            hop = NextHop(context, next!, http, bodyDropped, policy.LocationTrusted || IsSameOrigin(context.Url, next!), operationStarted);
            chain.Followed(target);
        }
    }

    private static string? RedirectTarget(TransferResult result) =>
        result.IsSuccess && result.Report is { ResponseCode: >= 300 and < 400, RedirectUrl: { } url }
            ? url
            : null;

    private static (CurlExitCode ExitCode, string Message)? Refusal(
        string target,
        bool pathAsIs,
        int followed,
        RedirectPolicy policy,
        out CurlUrl? next)
    {
        next = null;
        if (policy.MaxRedirects >= 0 && followed >= policy.MaxRedirects)
        {
            return (CurlExitCode.TooManyRedirects, $"Maximum ({policy.MaxRedirects}) redirects followed");
        }

        if (!CurlUrl.TryParse(target, pathAsIs, out next))
        {
            return (CurlExitCode.UrlMalformat, $"The redirect target URL could not be parsed: {UnparsableUrlReason(target)}");
        }

        return SchemeRefusal(next.Scheme, policy);
    }

    private static (CurlExitCode ExitCode, string Message)? SchemeRefusal(string scheme, RedirectPolicy policy)
    {
        if (!SchemesCurlParses.Contains(scheme))
        {
            return (CurlExitCode.UnsupportedProtocol, "The redirect target URL could not be parsed: Unsupported URL scheme");
        }

        return policy.AllowedSchemes.Contains(scheme)
            ? null
            : (CurlExitCode.UnsupportedProtocol, $"Protocol \"{scheme}\" is disabled (in redirect)");
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

    private static void RewindUpload(Stream? upload, long? start, bool bodyDropped)
    {
        if (!bodyDropped && start is { } position)
        {
            // A seekable upload's start was only recorded when the upload exists.
            upload!.Position = position;
        }
    }

    private static bool IsSameOrigin(CurlUrl first, CurlUrl next) =>
        string.Equals(first.Scheme, next.Scheme, StringComparison.Ordinal)
        && string.Equals(first.Host, next.Host, StringComparison.OrdinalIgnoreCase)
        && first.Port == next.Port;

    private static HttpRequestOptions HopHttp(HttpRequestOptions http, bool bodyDropped, bool sendCredentials)
    {
        HttpRequestOptions hopHttp = bodyDropped ? http with { Body = null } : http;
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
        bool bodyDropped,
        bool sendCredentials,
        long operationStarted) =>
        new()
        {
            Url = url,
            Output = first.Output,
            Upload = bodyDropped ? null : first.Upload,
            ResumeFrom = first.ResumeFrom,
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
            Http = HopHttp(http, bodyDropped, sendCredentials),
            TimeProvider = first.TimeProvider,
            CancellationToken = first.CancellationToken,
        };

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

        public TransferResult Merge(TransferResult outcome)
        {
            TransferReport report = lastReport ?? new TransferReport();
            return outcome with
            {
                Report = report with
                {
                    EffectiveUrl = effectiveUrl,
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
