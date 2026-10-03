using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads the head of an HTTP/1.0 or HTTP/1.1 response from an <see cref="IConnection" />:
/// the status line and headers, reading past any 1xx informational responses to the final
/// one, and failing with the exit code and message curl 8.21.0 reports.
/// </summary>
/// <remarks>
/// Lines may end in a carriage return and line feed or in a line feed alone, and a line that
/// starts with a blank continues the header before it. A peer that closes before a final
/// head's status line is whole, or inside a 1xx head, is an empty reply (exit 52); one that
/// closes among the final response's headers ends the head there, which curl 8.21.0 treats
/// as a complete response, never acting on the header it ends on (<see cref="HeadActedOn" />).
/// </remarks>
internal sealed class HttpResponseHeadReader
{
    private readonly HttpLineReader lines;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResponseHeadReader" /> class.
    /// </summary>
    /// <param name="connection">The connection the request was sent on.</param>
    internal HttpResponseHeadReader(IConnection connection)
    {
        lines = new(connection) { EndsBeforeRead = EndsAtRefusedHeader };
    }

    private readonly HttpResponseHeadBuilder builder = new();

    /// <summary>
    /// Gets a value indicating whether any byte of the response has arrived.
    /// </summary>
    internal bool HasReceived => lines.HasReceived;

    /// <summary>
    /// Gets a value indicating whether a <c>101 Switching Protocols</c> head was read before
    /// the final one, after which the connection no longer speaks HTTP/1.1 and is never reused
    /// (ADR-0050).
    /// </summary>
    internal bool SwitchedProtocols { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the final head read ended at its empty line rather than
    /// where the peer closed; curl 8.21.0 decides how the body ends only at that empty line.
    /// </summary>
    internal bool EndedAtEmptyLine { get; private set; }

    /// <summary>
    /// Gets where each head line is reported once it is accepted: one
    /// <see cref="ITransferEvents.ReportResponseHeader" /> per line, its line end included -
    /// every status line, 1xx heads' included, every header line and each head's empty line
    /// (ADR-0046). A status line is reported as received. A header line is held until the
    /// next line shows its header is whole, then reported after <see cref="HeaderReceived" />
    /// is told of the header, since curl 8.21.0 prints the <c>-v</c> lines a header causes
    /// before the header (measured, BL-468 Notes); a final head's whole headers are held
    /// further, until the head ends (<see cref="FindRefusal" />). The final head's empty line is held until
    /// <see cref="ReportHeldLines" />, since curl 8.21.0 prints the lines it decides at the
    /// end of the head before it (measured, BL-449 Notes). An HTTP/1.0 status line is
    /// preceded by <see cref="HttpConnectionInfoLines.AssumeCloseAfterBody" />.
    /// </summary>
    internal ITransferEvents Events { get; init; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets what is told of each head line right after it is reported to <see cref="Events" />,
    /// so an HTTP/2 stream's <c>--trace-config http/2</c> echo follows its <c>&lt;</c> line
    /// (<see cref="Http2FrameTrace.ResponseLineReported" />, BL-1205). By default nothing is.
    /// </summary>
    internal Action<byte[]> LineReported { get; init; } = static _ => { };

    /// <summary>
    /// Gets what is told of each head line right before it is reported to <see cref="Events" />,
    /// so an HTTP/3 stream's <c>--trace-config http/3</c> <c>header:</c> echo precedes its <c>&lt;</c>
    /// line (<see cref="Http3StreamTrace.ResponseLineReporting" />, BL-1208). By default nothing is.
    /// </summary>
    internal Action<byte[]> LineReporting { get; init; } = static _ => { };

    /// <summary>
    /// Gets what is told of each head's status line, 1xx heads' included, right after the line
    /// is reported to <see cref="Events" />, so a redirect's <c>Need to rewind upload for next
    /// request</c> follows it (BL-1213). By default nothing is.
    /// </summary>
    internal Action<HttpStatusLine>? StatusLineReported { get; init; }

    /// <summary>
    /// Gets what is told of each header of every head, 1xx heads' included, once it is whole -
    /// its continuation lines folded in - just before its lines are reported to
    /// <see cref="Events" />; a header whose head fails before it is whole is never told, nor
    /// the last header of a final head the peer closed among its headers (<see cref="HeadActedOn" />).
    /// It is told with the status line of the head the header belongs to.
    /// </summary>
    internal Action<HttpStatusLine, HttpResponseHeader> HeaderReceived { get; init; } = static (_, _) => { };

    /// <summary>
    /// Gets what finds the header of a final head that curl 8.21.0 refuses while it reads the
    /// head (<see cref="HttpResponseBodyReader.FindHeadRefusal" />), or <see langword="null" />
    /// when it refuses none. The final head's whole headers are held until the head ends or
    /// fails so it can be asked once of them - and, before a read from the connection, of the
    /// headers whole by then, when more are whole than it was last asked of, so a head the
    /// peer holds open after a refused header ends there (<see cref="EndsAtRefusedHeader" />);
    /// then those before the refused header are released
    /// and neither it nor any line after it is told to <see cref="HeaderReceived" /> or reported
    /// to <see cref="Events" />, the head's empty line included, since curl 8.21.0 stops reading
    /// the head at the refused header (measured, BL-475 Notes).
    /// </summary>
    internal Func<HttpResponseHead, HttpHeadRefusal?> FindRefusal { get; init; } = static _ => null;

    /// <summary>
    /// Gets a value indicating whether the heads are an HTTP/2 or HTTP/3 stream's, as
    /// <see cref="Http2StreamConnection" /> and <see cref="Http3StreamConnection" /> write them, whose status lines are parsed with
    /// <see cref="HttpStatusLine.ParseHttp2OrHttp3" /> rather than as HTTP/1.x.
    /// </summary>
    internal bool IsHttp2OrHttp3 { get; init; }

    /// <summary>
    /// Gets what tells, before each status line is parsed, whether the connection has switched
    /// to HTTP/2 after an h2c upgrade's <c>101</c> (<see cref="HttpH2cUpgradeConnection.IsUpgraded" />),
    /// so the line is an HTTP/2 stream's as for <see cref="IsHttp2OrHttp3" />. By default it never has.
    /// </summary>
    internal Func<bool> IsSwitchedToHttp2 { get; init; } = static () => false;

    /// <summary>
    /// Gets what decides, for each whole header of a final head that curl acts on, whether it
    /// and every header after it are deferred when the head is released: neither told to
    /// <see cref="HeaderReceived" /> nor reported until <see cref="ReleaseDeferredHeaders" />
    /// (or <see cref="ReportHeldLines" />), so the lines answering the head's challenges land
    /// before the header that caused them, as curl 8.21.0 writes a Negotiate context's failure
    /// (measured, BL-843 Notes). By default no header is deferred.
    /// </summary>
    internal Func<HttpStatusLine, HttpResponseHeader, bool> DefersFrom { get; init; } = static (_, _) => false;

    /// <summary>
    /// Gets the refused header of the final head <see cref="ReadAsync" /> read, as
    /// <see cref="FindRefusal" /> found it, or <see langword="null" /> when none was refused.
    /// </summary>
    internal HttpHeadRefusal? Refusal { get; private set; }

    private readonly List<byte[]> heldHeaderLines = [];

    private readonly List<HeldHeader> heldHeaders = [];

    private readonly List<HeldHeader> deferredHeaders = [];

    private bool heldHeaderKeepsHttp10Alive;

    private bool holdsWholeHeaders;

    private HttpStatusLine? headStatusLine;

    private int wholeHeadersAsked;

    private bool endedAtRefusedHeader;

    private byte[]? heldEmptyLine;

    /// <summary>
    /// Reports the lines <see cref="ReadAsync" /> still holds, once: the whole headers of a
    /// final head a failure cut off, each after <see cref="HeaderReceived" /> is told of it, then
    /// those of a header a failure cut off before it was whole, then the final head's empty
    /// line. Does nothing when none is held.
    /// </summary>
    internal void ReportHeldLines()
    {
        ReleaseHeldHeaders();
        ReleaseDeferredHeaders();
        ReportHeldHeaderLines();
        if (heldEmptyLine is { } bytes)
        {
            heldEmptyLine = null;
            ReportLine(bytes);
        }
    }

    /// <summary>
    /// Reports the last header of a final head the peer closed among its headers, which
    /// <see cref="ReadAsync" /> holds (<see cref="ReleaseHeadBeforeRefusal" />); does nothing for
    /// a head that ended at its empty line, whose held lines wait for <see cref="ReportHeldLines" />.
    /// </summary>
    internal void ReportHeaderHeldAtClose()
    {
        if (!EndedAtEmptyLine)
        {
            ReportHeldLines();
        }
    }

    /// <summary>
    /// Reads the response head.
    /// </summary>
    /// <param name="cancellationToken">Cancels every read.</param>
    /// <returns>The final response's head.</returns>
    /// <exception cref="HttpTransferException">
    /// The response is not HTTP/1.x (exit 1), a head line is malformed (exit 8), the peer
    /// closed before a final head (exit 52), a read failed or the heads grew too large in
    /// total (exit 56), or one line grew too large (exit 100) - unless a whole header of the
    /// final head before the failure is refused, when the head is returned with its
    /// <see cref="Refusal" /> instead.
    /// </exception>
    internal async ValueTask<HttpResponseHead> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            HttpStatusLine statusLine = await ReadStatusLineAsync(cancellationToken).ConfigureAwait(false);
            bool closed;
            try
            {
                closed = await ReadHeaderLinesAsync(statusLine, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpTransferException failure)
            {
                return HeadBeforeRefusalOrThrow(statusLine, failure);
            }

            if (!statusLine.IsInformational)
            {
                EndedAtEmptyLine = !closed;
                HttpResponseHead head = builder.Build(statusLine, closed ? [] : lines.TakeRemaining());
                Refusal = FindRefusal(closed && !endedAtRefusedHeader ? HeadBeforeLastHeader(head) : head);
                ReleaseHeadBeforeRefusal();
                return head;
            }

            if (closed)
            {
                throw EmptyReply();
            }
        }
    }

    /// <summary>
    /// Gives the part of the final head <see cref="ReadAsync" /> read that curl 8.21.0 acts on -
    /// frames the body by, decides the connection's reuse by, follows a redirect by: all of it,
    /// unless the peer closed among its headers with none refused, when the last header is left
    /// out. It is still reported, written and counted, but curl acts on a header only once a byte
    /// of the next line shows it whole, so never on the one the head ends on at close (measured,
    /// BL-483 Notes).
    /// </summary>
    /// <param name="head">The head <see cref="ReadAsync" /> returned, cut before any <see cref="Refusal" />.</param>
    /// <returns>The head curl acts on.</returns>
    internal HttpResponseHead HeadActedOn(HttpResponseHead head) =>
        EndedAtEmptyLine || Refusal is not null ? head : HeadBeforeLastHeader(head);

    /// <summary>
    /// Gives a head the peer closed among its headers without its last header, the one curl
    /// 8.21.0 never acts on (<see cref="HeadActedOn" />).
    /// </summary>
    private static HttpResponseHead HeadBeforeLastHeader(HttpResponseHead head) =>
        head.Headers.Count == 0 ? head : head.Before(head.Headers.Count - 1);

    /// <summary>
    /// Handles a head that failed before it ended: asks <see cref="FindRefusal" /> of the whole
    /// headers held so far and, when it refuses one, releases those before it, drops the rest
    /// and the header line the failure cut off, and gives the head as read, since curl 8.21.0
    /// stops reading the head at the refused header and so never reaches the later failure
    /// (measured, BL-479 Notes). A 1xx head holds no whole headers, so nothing in it is refused.
    /// </summary>
    /// <param name="statusLine">The failed head's status line.</param>
    /// <param name="failure">What failed the head.</param>
    /// <returns>The head, whose <see cref="Refusal" /> the caller fails with.</returns>
    /// <exception cref="HttpTransferException"><paramref name="failure" />, when no held header is refused.</exception>
    private HttpResponseHead HeadBeforeRefusalOrThrow(HttpStatusLine statusLine, HttpTransferException failure)
    {
        HttpResponseHead read = builder.Build(statusLine, []);
        HttpResponseHead head = new(statusLine, [.. heldHeaders.Select(held => held.Header)], read.HeadBytes, ReadOnlyMemory<byte>.Empty);
        Refusal = FindRefusal(head) ?? throw failure;
        heldHeaderLines.Clear();
        heldHeaderKeepsHttp10Alive = false;
        ReleaseHeadBeforeRefusal();
        return head;
    }

    /// <summary>
    /// Decides, before a read from the connection, whether the final head ends here because a
    /// header already whole is refused: curl 8.21.0 refuses a header as soon as a byte of the
    /// next line shows it is whole - a first byte that is not a blank, since a blank would fold
    /// the next line into it - and does not wait for the rest of the head, while a refused
    /// header with nothing after it yet waits for the peer (measured, BL-480 Notes). Asks
    /// <see cref="FindRefusal" /> only when more headers are whole than it was last asked of,
    /// so a head that arrives before its reader waits for it is never asked here, and each read
    /// at most once. The head then ends as if the peer
    /// had closed, and <see cref="ReadAsync" /> finds the refusal again in the head it builds.
    /// </summary>
    /// <param name="unfinishedLine">The bytes of the line after the last one read, so far.</param>
    /// <returns><see langword="true" /> when a whole header of the final head is refused.</returns>
    private bool EndsAtRefusedHeader(ReadOnlySpan<byte> unfinishedLine)
    {
        bool pendingIsWhole = ShowsPendingHeaderWhole(unfinishedLine);
        int wholeHeaders = heldHeaders.Count + (pendingIsWhole ? 1 : 0);
        if (!holdsWholeHeaders || wholeHeaders == wholeHeadersAsked)
        {
            return false;
        }

        wholeHeadersAsked = wholeHeaders;
        List<HttpResponseHeader> headers = [.. heldHeaders.Select(held => held.Header)];
        if (pendingIsWhole)
        {
            headers.Add(builder.PendingHeader);
        }

        endedAtRefusedHeader = FindRefusal(new HttpResponseHead(headStatusLine!, headers, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty)) is not null;
        return endedAtRefusedHeader;
    }

    /// <summary>
    /// Determines whether the unfinished line shows the header whose lines are held is whole:
    /// it has begun, and not with a blank that would fold it into that header.
    /// </summary>
    private bool ShowsPendingHeaderWhole(ReadOnlySpan<byte> unfinishedLine) =>
        heldHeaderLines.Count > 0 && unfinishedLine.Length > 0 && !HttpLine.IsBlank((char)unfinishedLine[0]);

    /// <summary>Tells <see cref="LineReporting" /> of one head line, reports it to <see cref="Events" />, then tells <see cref="LineReported" /> of it.</summary>
    private void ReportLine(byte[] bytes)
    {
        LineReporting(bytes);
        Events.ReportResponseHeader(bytes);
        LineReported(bytes);
    }

    private static HttpTransferException EmptyReply() =>
        new(CurlExitCode.GotNothing, HttpTransferMessages.EmptyReply);

    private async ValueTask<HttpStatusLine> ReadStatusLineAsync(CancellationToken cancellationToken)
    {
        byte[] bytes = await lines.ReadLineAsync(true, cancellationToken).ConfigureAwait(false) ?? throw EmptyReply();
        HttpLine line = HttpLine.Split(bytes);
        HttpStatusLine statusLine = IsHttp2OrHttp3 || IsSwitchedToHttp2() ? HttpStatusLine.ParseHttp2OrHttp3(line.Content) : HttpStatusLine.Parse(line.Content);
        SwitchedProtocols |= statusLine.StatusCode == 101;
        builder.StartHead(line);
        if (line.Content.StartsWith("HTTP/1.0", StringComparison.Ordinal))
        {
            Events.ReportInfo(HttpConnectionInfoLines.AssumeCloseAfterBody);
        }

        ReportLine(bytes);
        StatusLineReported?.Invoke(statusLine);
        return statusLine;
    }

    /// <summary>
    /// Reads header lines through the head's empty line, or until the peer closes, releasing
    /// each header's held lines once the header is whole; the empty line of a head that is not
    /// informational is held for <see cref="ReportHeldLines" /> rather than reported. In an
    /// HTTP/1.0 head, a header line that <see cref="HttpConnectionPersistence.KeepsHttp10Alive" />
    /// is preceded by <see cref="HttpConnectionInfoLines.Http10KeepAlive" />.
    /// </summary>
    /// <param name="statusLine">The head's status line.</param>
    /// <param name="cancellationToken">Cancels every read.</param>
    /// <returns><see langword="true" /> when the peer closed before the empty line.</returns>
    private async ValueTask<bool> ReadHeaderLinesAsync(HttpStatusLine statusLine, CancellationToken cancellationToken)
    {
        bool informational = statusLine.IsInformational;
        bool http10 = statusLine.Version == new Version(1, 0);
        holdsWholeHeaders = !informational;
        headStatusLine = statusLine;
        while (await lines.ReadLineAsync(false, cancellationToken).ConfigureAwait(false) is { } bytes)
        {
            HttpLine line = HttpLine.Split(bytes);
            if (line.IsEmpty)
            {
                builder.EndHead(line);
                ReleaseHeldHeader();
                heldEmptyLine = bytes;
                if (informational)
                {
                    ReportHeldLines();
                }

                return false;
            }

            builder.AddLine(line);
            if (!line.IsContinuation)
            {
                ReleaseHeldHeader();
                heldHeaderKeepsHttp10Alive = http10 && HttpConnectionPersistence.KeepsHttp10Alive(line.Content);
            }

            heldHeaderLines.Add(bytes);
        }

        builder.EndHeadAtClose();
        ReleaseHeldHeader();
        return true;
    }

    /// <summary>
    /// Releases the header the builder has just completed: in a 1xx head, tells
    /// <see cref="HeaderReceived" /> of it and reports its held lines; in the final head, holds
    /// it whole until the head ends (<see cref="FindRefusal" />). Does nothing when no header
    /// line is held.
    /// </summary>
    private void ReleaseHeldHeader()
    {
        if (heldHeaderLines.Count == 0)
        {
            return;
        }

        if (holdsWholeHeaders)
        {
            heldHeaders.Add(new HeldHeader(builder.LastHeader, [.. heldHeaderLines], heldHeaderKeepsHttp10Alive));
            heldHeaderLines.Clear();
            heldHeaderKeepsHttp10Alive = false;
            return;
        }

        HeaderReceived(headStatusLine!, builder.LastHeader);
        ReportHeldHeaderLines();
    }

    /// <summary>
    /// Releases the final head's whole headers before its refused header, or all of them when
    /// none is refused; a refused head drops the rest and its empty line unreported. A head the
    /// peer closed among its headers with none refused releases all but its last header, which
    /// it holds unacted on for <see cref="ReportHeldLines" />: reported then, but never told to
    /// <see cref="HeaderReceived" />, so its cookie is never stored (<see cref="HeadActedOn" />;
    /// measured, BL-484 Notes), and reported after the <c>-v</c> line of a failure the head
    /// before it causes (measured, BL-485 Notes).
    /// </summary>
    private void ReleaseHeadBeforeRefusal()
    {
        if (Refusal is { } refusal)
        {
            heldHeaders.RemoveRange(refusal.HeaderIndex, heldHeaders.Count - refusal.HeaderIndex);
            heldEmptyLine = null;
        }
        else if (!EndedAtEmptyLine && heldHeaders.Count > 0)
        {
            HeldHeader last = heldHeaders[^1] with { IsActedOn = false };
            heldHeaders.RemoveAt(heldHeaders.Count - 1);
            ReleaseHeldHeaders();
            heldHeaders.Add(last);
            return;
        }

        ReleaseHeldHeaders();
    }

    /// <summary>
    /// Tells <see cref="HeaderReceived" /> of each held whole header curl acts on and reports
    /// its lines, in the order they arrived, and holds none after.
    /// </summary>
    private void ReleaseHeldHeaders()
    {
        foreach (HeldHeader held in heldHeaders)
        {
            if (deferredHeaders.Count > 0 || (held.IsActedOn && DefersFrom(headStatusLine!, held.Header)))
            {
                deferredHeaders.Add(held);
            }
            else
            {
                ReleaseHeader(held);
            }
        }

        heldHeaders.Clear();
    }

    /// <summary>
    /// Tells <see cref="HeaderReceived" /> of each header <see cref="DefersFrom" /> deferred
    /// that curl acts on and reports its lines, in the order they arrived, and defers none
    /// after. Does nothing when none is deferred.
    /// </summary>
    internal void ReleaseDeferredHeaders()
    {
        foreach (HeldHeader deferred in deferredHeaders)
        {
            ReleaseHeader(deferred);
        }

        deferredHeaders.Clear();
    }

    private void ReleaseHeader(HeldHeader held)
    {
        if (held.IsActedOn)
        {
            HeaderReceived(headStatusLine!, held.Header);
        }

        ReportHeaderLines(held.Lines, held.KeepsHttp10Alive);
    }

    /// <summary>
    /// Reports the held lines of the header not yet released, and holds none after.
    /// </summary>
    private void ReportHeldHeaderLines()
    {
        ReportHeaderLines(heldHeaderLines, heldHeaderKeepsHttp10Alive);
        heldHeaderKeepsHttp10Alive = false;
        heldHeaderLines.Clear();
    }

    /// <summary>
    /// Reports one header's lines, preceded by
    /// <see cref="HttpConnectionInfoLines.Http10KeepAlive" /> when the header keeps an HTTP/1.0
    /// connection alive.
    /// </summary>
    private void ReportHeaderLines(IReadOnlyList<byte[]> headerLines, bool keepsHttp10Alive)
    {
        if (keepsHttp10Alive)
        {
            Events.ReportInfo(HttpConnectionInfoLines.Http10KeepAlive);
        }

        foreach (byte[] held in headerLines)
        {
            ReportLine(held);
        }
    }

    /// <summary>
    /// A whole header of the final head, held until the head ends.
    /// </summary>
    /// <param name="Header">The header, its continuation lines folded in.</param>
    /// <param name="Lines">Its lines as received, line ends included.</param>
    /// <param name="KeepsHttp10Alive">Whether it keeps an HTTP/1.0 connection alive.</param>
    private sealed record HeldHeader(HttpResponseHeader Header, byte[][] Lines, bool KeepsHttp10Alive)
    {
        /// <summary>
        /// Gets a value indicating whether curl 8.21.0 acts on the header, so
        /// <see cref="HeaderReceived" /> is told of it: all but the last header of a head the
        /// peer closed among its headers.
        /// </summary>
        internal bool IsActedOn { get; init; } = true;
    }
}
