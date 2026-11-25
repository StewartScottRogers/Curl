using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that turns a transfer exception or a timeout into the outcome and report curl would give.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reports a failed exchange's held header lines and the failure, and gives the attempt's
    /// outcome carrying the failure's exit code and the report as far as the exchange got.
    /// </summary>
    private static HttpAttemptOutcome OutcomeOfFailure(
        HttpRequestPlan plan,
        HttpExchange exchange,
        HttpResponseBodyReader body,
        HttpExchangeLog exchangeLog,
        HttpResponseHeadReader headReader,
        HttpRequestBodyWriter upload,
        bool reusedConnection,
        HttpTransferException failure)
    {
        ReportReturnedError(plan.Context.Events, failure);
        headReader.ReportHeldLines();
        ReportReceiveFailure(plan.Context.Events, failure);
        ReportBodyFailure(plan.Context.Events, failure);
        exchangeLog.Failed(failure.ExitCode, failure);
        if (failure.ExitCode == CurlExitCode.HttpReturnedError && plan.Options.Fail == HttpFailMode.Fail)
        {
            exchange.RedirectUrl = null;
        }

        TransferResult failed = TransferResult.Failure(failure.ExitCode, failure.Message, body.BytesWritten)
            with
        {
            Report = WithoutFirstByteWhenHttp09Refused(WithHeadBeforeFailure(exchange.Report(body), failure), failure),
        };
        HttpAttemptOutcome outcome = FailedOutcome(plan, reusedConnection, upload, headReader, failure, failed);
        ReportEmptyReply(plan.Context.Events, failure, outcome);
        return outcome;
    }

    /// <summary>
    /// Drops the first-byte time of a reply refused as HTTP/0.9: curl 8.22.0 reports <c>%{time_starttransfer}</c> 0 for it,
    /// as the bytes were never a response (STD-146).
    /// </summary>
    private static TransferReport? WithoutFirstByteWhenHttp09Refused(TransferReport? report, HttpTransferException failure) =>
        report is { Timings: { } timings } && failure.Message == HttpTransferMessages.Http09NotAllowed
            ? report with { Timings = timings with { FirstByteReceived = null } }
            : report;

    /// <summary>
    /// Echoes <c>-f</c>'s <c>The requested URL returned error: N</c> and <c>--max-filesize</c>'s messages as a <c>-v</c> line, as
    /// curl 8.22.0's <c>failf</c> does, before the head's held empty line (measured, BL-3617).
    /// </summary>
    private static void ReportReturnedError(ITransferEvents events, HttpTransferException failure)
    {
        if (failure.ExitCode is CurlExitCode.HttpReturnedError or CurlExitCode.FilesizeExceeded)
        {
            events.ReportInfo(failure.Message);
        }
    }

    /// <summary>
    /// Echoes the failf line of a body that failed as a <c>-v</c> line, as curl 8.22.0 does: a bad
    /// gzip or deflate stream, a bad chunk (its line and the one naming the chunk-decoder error),
    /// and a body the peer left short or a chunked body it cut off (measured, BL-3952).
    /// </summary>
    private static void ReportBodyFailure(ITransferEvents events, HttpTransferException failure)
    {
        string message = failure.Message;
        if (failure.ExitCode == CurlExitCode.RecvError)
        {
            ReportChunkFailure(events, message);
        }
        else if (failure.ExitCode == CurlExitCode.BadContentEncoding
            ? HttpTransferMessages.IsContentUnencodingError(message)
            : failure.ExitCode == CurlExitCode.PartialFile
                && (message == HttpTransferMessages.ChunkedBodyIncomplete || HttpTransferMessages.IsBodyBytesMissing(message)))
        {
            events.ReportInfo(message);
        }
    }

    private static void ReportChunkFailure(ITransferEvents events, string message)
    {
        string? followUp = HttpTransferMessages.ChunkSizeFollowUpLine(message);
        if (followUp is not null)
        {
            events.ReportInfo(message);
            events.ReportInfo(followUp);
        }
        else if (message == HttpTransferMessages.MalformedChunkedEncoding)
        {
            events.ReportInfo(message);
        }
    }

    /// <summary>
    /// Echoes <c>Empty reply from server</c> as a <c>-v</c> line, as curl 8.22.0's <c>failf</c>
    /// does, when the transfer ends with it rather than sending the request again on a new
    /// connection (measured, BL-3617).
    /// </summary>
    private static void ReportEmptyReply(ITransferEvents events, HttpTransferException failure, HttpAttemptOutcome outcome)
    {
        if (failure.Message == HttpTransferMessages.EmptyReply && outcome.Retry is null)
        {
            events.ReportInfo(failure.Message);
        }
    }

    /// <summary>
    /// Puts the response code, HTTP version and header size of the head a failure cut short on
    /// <paramref name="report" />, as libcurl 8.22.0 keeps them after a header line it refuses (BL-3195);
    /// <paramref name="report" /> itself when the head was read whole or the failure came before its status line.
    /// </summary>
    private static TransferReport WithHeadBeforeFailure(TransferReport report, HttpTransferException failure) =>
        WithStatusLineBeforeFailure(WithAnsweredHeaders(report, failure), failure);

    /// <summary>Puts the headers of the 401 or 407 whose challenge went unanswered on <paramref name="report" />, which curl keeps (BL-5331).</summary>
    private static TransferReport WithAnsweredHeaders(TransferReport report, HttpTransferException failure) =>
        failure.AnsweredHeadersBeforeFailure is { } headers ? report with { ResponseHeaders = headers } : report;

    private static TransferReport WithStatusLineBeforeFailure(TransferReport report, HttpTransferException failure) =>
        report.ResponseCode != 0 || failure.StatusLineBeforeFailure is not { } statusLine
            ? WithReceiveErrno(report, failure)
            : WithHeadersBeforeFailure(report, failure, statusLine, WithReceiveErrno(report, failure) with
            {
                ResponseCode = statusLine.StatusCode,
                HttpVersion = statusLine.Version,
                HeaderSize = report.HeaderSize + failure.HeadBytesBeforeFailure.Length,
                ResponseHeadersStored = Math.Max(report.ResponseHeadersStored, failure.HeadersStoredBeforeFailure),
                ContentType = failure.ContentTypeBeforeFailure ?? report.ContentType,
            });

    /// <summary>
    /// Gives <paramref name="cut" /> the header lines of the head a failure cut short. When that head is a 1xx
    /// one they are counted (<see cref="TransferReport.InformationalHeaderCount" />, for <c>%{num_headers}</c>)
    /// but kept out of <see cref="TransferReport.ResponseHeaders" />, which <c>%{header_json}</c> and
    /// <c>%header{}</c> read: curl 8.22.0 and 8.18.0 show none of them (STD-147, BL-5466).
    /// </summary>
    private static TransferReport WithHeadersBeforeFailure(TransferReport report, HttpTransferException failure, HttpStatusLine statusLine, TransferReport cut)
    {
        if (failure.HeadBytesBeforeFailure.Length == 0)
        {
            return cut with { ResponseHeaders = report.ResponseHeaders };
        }

        KeyValuePair<string, string>[] headers = HeadersBeforeFailure(failure.HeadBytesBeforeFailure);
        return statusLine.IsInformational
            ? cut with { ResponseHeaders = [], InformationalHeaderCount = InformationalHeaderLineCount(failure.HeadBytesBeforeFailure, headers.Length) }
            : cut with { ResponseHeaders = headers };
    }

    /// <summary>
    /// Gives the whole header lines a failure let through before it cut the head short, from the
    /// head's bytes after its status line: curl stores each header line as it writes it, so
    /// <c>%{num_headers}</c> counts them (CGR-135 <c>cltwo</c>, curl 8.18.0 with nghttp2 1.68
    /// measured, BL-4005 Notes). A line cut off before its end is not one of them. Only the last
    /// head's lines count, when 1xx heads came before it (BL-5401).
    /// </summary>
    private static KeyValuePair<string, string>[] HeadersBeforeFailure(byte[] headBytes)
    {
        List<string> lines = [.. System.Text.Encoding.Latin1.GetString(headBytes).Split('\n').SkipLast(1).Select(line => line.TrimEnd('\r'))];
        if (lines.LastOrDefault() == string.Empty)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return [.. lines.Skip(lines.LastIndexOf(string.Empty) + 1).Skip(1).Select(HeaderOfLine)];
    }

    /// <summary>
    /// What <c>%{num_headers}</c> counts after 1xx heads cut short: the lines of the last head that has any, so
    /// <c>102</c>, <c>102</c> with <c>A: b</c>, <c>102</c> counts one as curl does (STD-148, BL-5467);
    /// <paramref name="lastHeadCount" /> when the last head has some or no head has.
    /// </summary>
    private static int InformationalHeaderLineCount(byte[] headBytes, int lastHeadCount)
    {
        if (lastHeadCount > 0)
        {
            return lastHeadCount;
        }

        return System.Text.Encoding.Latin1.GetString(headBytes).Split("\r\n\r\n").Select(head => Math.Max(0, head.Split("\r\n").Length - 1)).LastOrDefault(count => count > 0);
    }

    /// <summary>The key the engine reads a cancelled transfer's report from, spelled as <c>EndPointReportingProtocolHandler.ReportDataKey</c> spells it (BL-5862).</summary>
    private const string CancelledReportDataKey = "Curl.Engine.TransferReport";

    private static KeyValuePair<string, string> HeaderOfLine(string line)
    {
        string name = line.Split(':')[0];
        return KeyValuePair.Create(name, line[Math.Min(name.Length + 1, line.Length)..].Trim());
    }

    /// <summary>
    /// Puts the socket error number of a failed receive on <paramref name="report" /> as its
    /// <see cref="TransferReport.OsErrno" />, as libcurl 8.22.0 keeps it (BL-3329);
    /// <paramref name="report" /> itself when no socket error failed the receive.
    /// </summary>
    private static TransferReport WithReceiveErrno(TransferReport report, HttpTransferException failure) =>
        failure.OsErrno == 0 ? report : report with { OsErrno = failure.OsErrno };

    /// <summary>
    /// Reports a timed-out exchange's held header lines, and gives the attempt's outcome: exit
    /// 28 with the report as far as the exchange got, on a connection that is not kept.
    /// </summary>
    private static HttpAttemptOutcome OutcomeOfTimeout(
        HttpRequestPlan plan,
        HttpExchange exchange,
        HttpResponseBodyReader body,
        HttpExchangeLog exchangeLog,
        HttpResponseHeadReader headReader,
        OperationCanceledException canceled)
    {
        headReader.ReportHeldLines();
        exchangeLog.Failed(CurlExitCode.OperationTimedOut, canceled);
        string message = HttpTransferMessages.OperationTimedOut(plan.Deadline.OperationElapsedMilliseconds, body.BytesWritten, body.ExpectedLength);
        plan.Context.Events.ReportInfo(message);
        TransferResult timedOut = TransferResult.Failure(CurlExitCode.OperationTimedOut, message, body.BytesWritten)
            with
        { Report = exchange.Report(body) };
        return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false);
    }
}
