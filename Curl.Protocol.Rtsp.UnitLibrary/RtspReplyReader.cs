using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Reads an RTSP reply from the connection: the head line by line, each line written to the
/// header output (<c>-i</c>, <c>-I</c>, <c>-D</c>) once it is read, then the body, which is
/// counted and discarded (ADR-0169).
/// </summary>
/// <remarks>
/// Measured on curl 8.21.0 (BL-591). A header line is read only once a byte after it has
/// arrived, or when it is the blank line that ends the head: a server that closes after
/// <c>RTSP/1.0 200 OK\r\nCSeq: 1\r\n</c> leaves the <c>CSeq</c> line unread, so the reply's
/// <c>CSeq</c> is 0, while one that closes after <c>…CSeq: 1\r\nPubl</c> has sent a <c>CSeq</c>
/// of 1. A header line followed by continuation lines (a space or tab first) is read as one line
/// with them joined, once a byte after the last has arrived (BL-840). The bytes still unread
/// when the server closes are written to the header output unchecked, continuation lines joined. A reply whose first bytes are not <c>RTSP/</c>, exactly, fails with 52,
/// <c>Empty reply from server</c>, nothing written; so does a connection closed before any
/// byte. A head longer than <see cref="MaximumHeadLength" /> fails with 100, as the HTTP
/// library's does. A failed read fails with 56, a failed header write with 23.
/// </remarks>
internal static class RtspReplyReader
{
    /// <summary>The most bytes a reply head may take before the transfer fails with 100.</summary>
    internal const int MaximumHeadLength = 102400;

    /// <summary>The exit 52 message for a reply that is empty or not RTSP.</summary>
    internal const string EmptyReply = "Empty reply from server";

    /// <summary>The exit 100 message for a head longer than <see cref="MaximumHeadLength" />.</summary>
    internal const string HeadTooLarge = "A value or data field grew larger than allowed";

    private const int BufferSize = 16384;

    /// <summary>
    /// Reads the reply head, writing each line to <paramref name="headerOutput" /> and
    /// reporting it to <paramref name="events" /> for <c>-v</c>, except the blank line that
    /// ends it, which is returned as <see cref="RtspReplyHead.EndLine" /> for the caller to
    /// report after any <c>-f</c> refusal, as curl 8.21.0 does (BL-593).
    /// </summary>
    /// <param name="connection">The connection the request was sent on.</param>
    /// <param name="session">The transfer's session state, which keeps or checks each <c>Session</c> header.</param>
    /// <param name="headerOutput">Where the head is written, or <see langword="null" /> for nowhere.</param>
    /// <param name="events">Where each head line is reported.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="log">Receives each header line's name.</param>
    /// <param name="maxFileSize">The transfer's <c>--max-filesize</c> limit, handed to <see cref="RtspReplyHeadParser" />.</param>
    /// <returns>What was read of the head.</returns>
    /// <exception cref="RtspTransferException">The reply is refused, or a read or write failed.</exception>
    internal static async ValueTask<RtspReplyHead> ReadHeadAsync(IConnection connection, RtspSessionState session, Stream? headerOutput, ITransferEvents events, CancellationToken cancellationToken, RtspTransferLog log, long? maxFileSize)
    {
        RtspReplyHeadParser parser = new(session, maxFileSize);
        byte[] buffer = new byte[BufferSize];
        int received = 0;
        int processed = 0;
        while (true)
        {
            RefuseUnlessRtsp(parser, buffer.AsSpan(0, received));
            int lineEnd;
            while ((lineEnd = ReadableLineEnd(parser, buffer.AsSpan(0, received), processed)) > 0)
            {
                byte[] line = RtspHeaderFolding.Unfold(buffer.AsSpan(processed, lineEnd - processed));
                parser.Accept(line);
                ReportOverflow(parser, events);
                await WriteAsync(headerOutput, line, cancellationToken).ConfigureAwait(false);
                if (parser.IsComplete)
                {
                    return Head(parser, lineEnd, line, buffer[lineEnd..received]);
                }

                events.ReportResponseHeader(line);
                log.HeaderRead(line);
                processed = lineEnd;
            }

            buffer = MakeRoom(buffer, received);
            int read = await ReceiveAsync(connection, buffer.AsMemory(received, Math.Min(buffer.Length, MaximumHeadLength) - received), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return await EndEarlyAsync(parser, headerOutput, events, buffer.AsMemory(processed, received - processed), received, cancellationToken).ConfigureAwait(false);
            }

            received += read;
        }
    }

    /// <summary>
    /// Reads and discards the body: up to <paramref name="contentLength" /> bytes, starting with
    /// those already received after the head, or fewer when the server closes first, which curl
    /// 8.21.0 does not treat as an error (measured, BL-591).
    /// </summary>
    /// <param name="connection">The connection the reply arrives on.</param>
    /// <param name="alreadyReceived">The bytes received after the head.</param>
    /// <param name="contentLength">The reply's <c>Content-Length</c>.</param>
    /// <param name="progress">Where the running count is reported.</param>
    /// <param name="events">Where each piece of the body is reported as received data, for <c>-v</c> and <c>--trace</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many body bytes were read.</returns>
    /// <exception cref="RtspTransferException">A read failed.</exception>
    internal static async ValueTask<long> DiscardBodyAsync(
        IConnection connection,
        byte[] alreadyReceived,
        long contentLength,
        ITransferProgress progress,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        long bodyRead = Math.Min(alreadyReceived.Length, contentLength);
        ReportReceived(events, alreadyReceived.AsSpan(0, (int)bodyRead));
        byte[] buffer = new byte[BufferSize];
        int read = 1;
        while (bodyRead < contentLength && read > 0)
        {
            progress.ReportDownloaded(bodyRead, contentLength);
            read = await ReceiveAsync(connection, buffer.AsMemory(0, (int)Math.Min(buffer.Length, contentLength - bodyRead)), cancellationToken).ConfigureAwait(false);
            ReportReceived(events, buffer.AsSpan(0, read));
            bodyRead += read;
        }

        progress.ReportDownloaded(bodyRead, contentLength);
        return bodyRead;
    }

    /// <summary>
    /// Reports <c>Overflow Content-Length: value</c> before a <c>Content-Length</c> line whose
    /// number is too large for 64 bits, as curl 8.21.0 does (measured, BL-1403).
    /// </summary>
    private static void ReportOverflow(RtspReplyHeadParser parser, ITransferEvents events)
    {
        if (parser.LineOverflowedContentLength)
        {
            events.ReportInfo(RtspReplyHeadParser.OverflowContentLength);
        }
    }

    private static void ReportReceived(ITransferEvents events, ReadOnlySpan<byte> bytes)
    {
        if (!bytes.IsEmpty)
        {
            events.ReportDataReceived(bytes);
        }
    }

    /// <summary>
    /// Fails with 52 when the bytes received so far cannot begin <c>RTSP/</c> and no status line
    /// has been read.
    /// </summary>
    private static void RefuseUnlessRtsp(RtspReplyHeadParser parser, ReadOnlySpan<byte> received)
    {
        ReadOnlySpan<byte> prefix = "RTSP/"u8;
        int length = Math.Min(received.Length, prefix.Length);
        if (!parser.HasStatus && !received[..length].SequenceEqual(prefix[..length]))
        {
            throw new RtspTransferException(CurlExitCode.GotNothing, EmptyReply);
        }
    }

    /// <summary>
    /// Finds the end of the next line that can be read: the blank line that ends the head, or a
    /// line a byte has arrived after that does not begin a continuation line. A header line's
    /// continuation lines are part of it, so it ends where its last continuation line ends.
    /// </summary>
    /// <returns>The offset just past the line's last line feed, or 0 when no line can be read yet.</returns>
    private static int ReadableLineEnd(RtspReplyHeadParser parser, ReadOnlySpan<byte> received, int processed)
    {
        int lineEnd = LineEnd(received, processed);
        if (lineEnd > 0 && IsBlankLine(parser, received[processed..lineEnd]))
        {
            return lineEnd;
        }

        lineEnd = parser.HasStatus ? EndOfContinuations(received, lineEnd) : lineEnd;
        return lineEnd < received.Length ? lineEnd : 0;
    }

    /// <summary>
    /// Follows a header line's continuation lines, each starting with a space or a tab, to the
    /// end of the last one received whole.
    /// </summary>
    /// <param name="received">The bytes received so far.</param>
    /// <param name="lineEnd">The end of the header line, or 0 when it has not been received whole.</param>
    /// <returns>The end of the last continuation line, or 0 when one has not been received whole.</returns>
    private static int EndOfContinuations(ReadOnlySpan<byte> received, int lineEnd)
    {
        while (lineEnd > 0 && lineEnd < received.Length && RtspHeaderFolding.IsBlank(received[lineEnd]))
        {
            lineEnd = LineEnd(received, lineEnd);
        }

        return lineEnd;
    }

    /// <summary>Finds the end of the line that starts at <paramref name="start" />.</summary>
    /// <returns>The offset just past the first line feed at or after <paramref name="start" />, or 0 when there is none.</returns>
    private static int LineEnd(ReadOnlySpan<byte> received, int start)
    {
        int lineFeed = received[start..].IndexOf((byte)'\n');
        return lineFeed < 0 ? 0 : start + lineFeed + 1;
    }

    /// <summary>Determines whether <paramref name="line" /> is the blank line that ends the head.</summary>
    private static bool IsBlankLine(RtspReplyHeadParser parser, ReadOnlySpan<byte> line) =>
        parser.HasStatus && (line.Length == 1 || (line.Length == 2 && line[0] == '\r'));

    /// <summary>
    /// Ends a head the server closed before its blank line: 52 when nothing, or nothing that can
    /// begin <c>RTSP/</c>, arrived; otherwise the unread bytes are written, a header line's
    /// continuation lines joined to it (measured, BL-840) but nothing else changed or checked,
    /// and the head is returned incomplete. The unread bytes are reported as one head line, as curl
    /// 8.21.0's <c>-v</c> writes them.
    /// </summary>
    private static async ValueTask<RtspReplyHead> EndEarlyAsync(
        RtspReplyHeadParser parser,
        Stream? headerOutput,
        ITransferEvents events,
        ReadOnlyMemory<byte> unread,
        int received,
        CancellationToken cancellationToken)
    {
        if (received == 0 || (!parser.HasStatus && received < "RTSP/".Length))
        {
            throw new RtspTransferException(CurlExitCode.GotNothing, EmptyReply);
        }

        byte[] lines = parser.HasStatus ? RtspHeaderFolding.Unfold(unread.Span) : unread.ToArray();
        await WriteAsync(headerOutput, lines, cancellationToken).ConfigureAwait(false);
        events.ReportResponseHeader(lines);
        return Head(parser, received, endLine: null, []);
    }

    private static RtspReplyHead Head(RtspReplyHeadParser parser, int length, byte[]? endLine, byte[] remaining) =>
        new(parser.StatusCode, parser.SequenceNumber, parser.ContentLength, length, endLine, remaining);

    private static byte[] MakeRoom(byte[] buffer, int received)
    {
        if (received >= MaximumHeadLength)
        {
            throw new RtspTransferException(CurlExitCode.TooLarge, HeadTooLarge);
        }

        if (received < buffer.Length)
        {
            return buffer;
        }

        Array.Resize(ref buffer, buffer.Length * 2);
        return buffer;
    }

    private static async ValueTask<int> ReceiveAsync(IConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw RtspIoFailures.ReceiveFailed(exception);
        }
    }

    private static async ValueTask WriteAsync(Stream? headerOutput, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (headerOutput is null || bytes.IsEmpty)
        {
            return;
        }

        try
        {
            await headerOutput.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw RtspIoFailures.WriteFailed(bytes.Length, exception);
        }
    }
}
