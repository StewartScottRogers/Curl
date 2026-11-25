using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that formats the request head and reads the response head.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reads the response head; on a reused connection, a reply that cannot begin <c>HTTP/</c>
    /// fails with exit 8, <c>Invalid status line</c>, as curl 8.21.0 never takes HTTP/0.9 on
    /// a reused connection (upstream test1479), rather than with exit 1. A failure that carries
    /// the head bytes accepted before it writes them to the header output first (test580).
    /// </summary>
    private static async ValueTask<HttpResponseHead> ReadHeadAsync(HttpResponseHeadReader headReader, Stream? headerOutput, bool newConnection, CancellationToken cancellationToken)
    {
        HttpTransferException headFailure;
        try
        {
            return await headReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpTransferException failure) when (!newConnection && failure.Message == HttpTransferMessages.Http09NotAllowed)
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.InvalidStatusLine);
        }
        catch (HttpTransferException failure) when (failure.HeadBytesBeforeFailure.Length > 0)
        {
            headFailure = failure;
        }

        await WriteHeadersAsync(headerOutput, headFailure.HeadBytesBeforeFailure, cancellationToken).ConfigureAwait(false);
        throw headFailure;
    }

    /// <summary>
    /// Fails a response over HTTP/1.x whose major version differs from the one the connection's
    /// earlier response named (upstream test471), and else records its version on the
    /// connection for the next request (<see cref="Http1VersionSeen" />).
    /// </summary>
    private static void ThrowIfVersionMismatched(IConnection transport, bool overStreams, HttpResponseHead head)
    {
        if (overStreams)
        {
            return;
        }

        Version version = Http1VersionSeen.VersionNamed(head.HeadBytes.Span, head.StatusLine.Version);
        Http1VersionSeen.ThrowIfMismatched(transport, version);
        Http1VersionSeen.Record(transport, version);
    }

    /// <summary>
    /// Formats the request head: the HTTP/1.1 head, asking to upgrade to h2c when
    /// <paramref name="upgradesToH2c" />, or over HTTP/2 or HTTP/3 the same head naming
    /// <c>HTTP/2</c> or <c>HTTP/3</c> in its request line, which is what is reported sent and what the stream
    /// turns into its HEADERS (<see cref="Http2RequestHeaders" />).
    /// </summary>
    /// <remarks>
    /// On a connection whose earlier response was HTTP/1.0 the head is formatted as for
    /// <c>-0</c>, as curl downgrades the connection (upstream test1074), and so is a hop after an
    /// earlier hop's HTTP/1.0 response (<see cref="HttpRequestOptions.EarlierResponseWasHttp10" />).
    /// </remarks>
    private byte[] FormatRequestHead(HttpRequestPlan plan, IHttpStreamSession? streams, bool upgradesToH2c, bool downgradesToHttp10)
    {
        ITransferContext context = plan.Context;
        byte[] head = HttpRequestHeadFormatter.Format(
            context.Url,
            downgradesToHttp10 ? plan.Options with { Version = HttpVersionPreference.Http10 } : plan.Options,
            context.NoBody,
            plan.Authorization,
            CookieHeaderFor(context, plan.Options),
            plan.ForwardProxy is not null,
            plan.ProxyAuthorization,
            HttpRangeHeader.ValueFor(context, plan.Framing.Body is not null),
            context.TimeCondition,
            plan.Framing,
            upgradesToH2c,
            context.Credentials,
            plan.RereadsUrl);
        return streams is null ? head : Http2RequestHeaders.WithRequestLineVersion(head, streams.VersionName);
    }

    /// <summary>
    /// Reports <c>using HTTP/1.x</c>, <c>using HTTP/2</c> or <c>using HTTP/3</c>, before the first request on a
    /// connection this transfer opened; curl 8.21.0 prints nothing of the kind for a connection
    /// it reuses (measured, BL-407 Notes).
    /// </summary>
    private static void ReportProtocolChosen(ITransferEvents events, bool newConnection, IHttpStreamSession? streams)
    {
        if (newConnection)
        {
            events.ReportInfo(streams?.UsingLine ?? HttpConnectionInfoLines.UsingHttp1);
        }
    }

    /// <summary>
    /// Logs the version the exchange uses to the diagnostic log (BL-922), and, on a connection
    /// this transfer opened, a downgrade from the version the command line asked for.
    /// </summary>
    private static void LogVersionChosen(HttpExchangeLog exchangeLog, HttpRequestPlan plan, IConnection transport, bool newConnection, IHttpStreamSession? streams)
    {
        string versionName = streams?.VersionName ?? HttpExchangeLog.Http1VersionName;
        exchangeLog.VersionChosen(versionName, newConnection);
        if (newConnection)
        {
            exchangeLog.DowngradeOf(plan.Options.Version, transport.IsSecure, versionName);
        }
    }

    /// <summary>
    /// Logs the end of a successful exchange to the diagnostic log (BL-922), reading the clock
    /// only when <c>info</c> lines are written so a disabled log changes no timing.
    /// </summary>
    private static void LogExchanged(HttpExchangeLog exchangeLog, TimeProvider timeProvider, long? requestReady, HttpResponseHead head, HttpResponseBodyReader body)
    {
        if (exchangeLog.LogsInfo)
        {
            exchangeLog.Exchanged(head.StatusLine.StatusCode, body.BytesWritten, timeProvider.GetElapsedTime(requestReady!.Value));
        }
    }

    /// <summary>
    /// Reports <c>no chunk, no close, no size. Assume close to signal end</c> for a head whose
    /// body can only end when the server closes, as curl 8.21.0 does once the head is accepted:
    /// before the <c>-f</c> failure and the head's empty line; nothing for a head the peer
    /// closed before its empty line (measured, BL-467 Notes).
    /// </summary>
    private static void ReportNoEndOfMessageIndicator(HttpRequestPlan plan, HttpResponseHead head, HttpResponseHeadReader headReader)
    {
        // curl 8.22.0 writes no such line for a final 101's body (measured, BL-3599 Notes).
        if (headReader.EndedAtEmptyLine
            && head.StatusLine.StatusCode != 101
            && HttpConnectionPersistence.LacksEndOfMessageIndicator(head, plan.Context.NoBody, plan.Options.IgnoreContentLength))
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.NoEndOfMessageIndicator);
        }
    }
}
