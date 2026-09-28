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
/// as a complete response.
/// </remarks>
/// <param name="connection">The connection the request was sent on.</param>
internal sealed class HttpResponseHeadReader(IConnection connection)
{
    private readonly HttpLineReader lines = new(connection);

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
    /// before the header (measured, BL-468 Notes). The final head's empty line is held until
    /// <see cref="ReportHeldLines" />, since curl 8.21.0 prints the lines it decides at the
    /// end of the head before it (measured, BL-449 Notes). An HTTP/1.0 status line is
    /// preceded by <see cref="HttpConnectionInfoLines.AssumeCloseAfterBody" />.
    /// </summary>
    internal ITransferEvents Events { get; init; } = NoTransferEvents.Instance;

    /// <summary>
    /// Gets what is told of each header of every head, 1xx heads' included, once it is whole -
    /// its continuation lines folded in - just before its lines are reported to
    /// <see cref="Events" />; a header whose head fails before it is whole is never told.
    /// </summary>
    internal Action<HttpResponseHeader> HeaderReceived { get; init; } = static _ => { };

    private readonly List<byte[]> heldHeaderLines = [];

    private bool heldHeaderKeepsHttp10Alive;

    private byte[]? heldEmptyLine;

    /// <summary>
    /// Reports the lines <see cref="ReadAsync" /> still holds, once: those of a header a
    /// failure cut off before it was whole, then the final head's empty line. Does nothing when
    /// none is held.
    /// </summary>
    internal void ReportHeldLines()
    {
        ReportHeldHeaderLines();
        if (heldEmptyLine is { } bytes)
        {
            heldEmptyLine = null;
            Events.ReportResponseHeader(bytes);
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
    /// total (exit 56), or one line grew too large (exit 100).
    /// </exception>
    internal async ValueTask<HttpResponseHead> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            HttpStatusLine statusLine = await ReadStatusLineAsync(cancellationToken).ConfigureAwait(false);
            bool closed = await ReadHeaderLinesAsync(statusLine, cancellationToken).ConfigureAwait(false);
            if (!statusLine.IsInformational)
            {
                EndedAtEmptyLine = !closed;
                return builder.Build(statusLine, closed ? [] : lines.TakeRemaining());
            }

            if (closed)
            {
                throw EmptyReply();
            }
        }
    }

    private static HttpTransferException EmptyReply() =>
        new(CurlExitCode.GotNothing, HttpTransferMessages.EmptyReply);

    private async ValueTask<HttpStatusLine> ReadStatusLineAsync(CancellationToken cancellationToken)
    {
        byte[] bytes = await lines.ReadLineAsync(true, cancellationToken).ConfigureAwait(false) ?? throw EmptyReply();
        HttpLine line = HttpLine.Split(bytes);
        HttpStatusLine statusLine = HttpStatusLine.Parse(line.Content);
        SwitchedProtocols |= statusLine.StatusCode == 101;
        builder.StartHead(line);
        if (line.Content.StartsWith("HTTP/1.0", StringComparison.Ordinal))
        {
            Events.ReportInfo(HttpConnectionInfoLines.AssumeCloseAfterBody);
        }

        Events.ReportResponseHeader(bytes);
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
    /// Tells <see cref="HeaderReceived" /> of the header the builder has just completed and
    /// reports its held lines; does nothing when no header line is held.
    /// </summary>
    private void ReleaseHeldHeader()
    {
        if (heldHeaderLines.Count > 0)
        {
            HeaderReceived(builder.LastHeader);
            ReportHeldHeaderLines();
        }
    }

    /// <summary>
    /// Reports the held header lines, preceded by
    /// <see cref="HttpConnectionInfoLines.Http10KeepAlive" /> when the header keeps an HTTP/1.0
    /// connection alive, and holds none after.
    /// </summary>
    private void ReportHeldHeaderLines()
    {
        if (heldHeaderKeepsHttp10Alive)
        {
            heldHeaderKeepsHttp10Alive = false;
            Events.ReportInfo(HttpConnectionInfoLines.Http10KeepAlive);
        }

        foreach (byte[] held in heldHeaderLines)
        {
            Events.ReportResponseHeader(held);
        }

        heldHeaderLines.Clear();
    }
}
