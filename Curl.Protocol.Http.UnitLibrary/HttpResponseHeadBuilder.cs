using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Collects the heads of one exchange line by line: folds continuation lines into the
/// header before them, splits each header at its colon, and holds the combined size of
/// every head, 1xx heads included, to curl's limit.
/// </summary>
internal sealed class HttpResponseHeadBuilder
{
    /// <summary>
    /// The combined head size curl 8.21.0 allows: heads totalling 307200 bytes are accepted
    /// and one more byte fails with exit 56 (measured, BL-169).
    /// </summary>
    internal const int MaximumHeadSize = 307200;

    private readonly StringBuilder headText = new();

    private readonly List<HttpResponseHeader> headers = [];

    private readonly StringBuilder pendingHeader = new();

    private bool hasPendingHeader;

    private string pendingTerminator = string.Empty;

    private string? location;

    /// <summary>
    /// Gets the header added last to the current head, its continuation lines folded in; valid
    /// once <see cref="AddLine" />, <see cref="EndHead" /> or <see cref="EndHeadAtClose" /> has
    /// completed a header of it.
    /// </summary>
    internal HttpResponseHeader LastHeader => headers[^1];

    /// <summary>
    /// Gets the header the current head's last header line started, its continuation lines so
    /// far folded in, as it would be added were it whole now; valid once <see cref="AddLine" />
    /// has started a header that no later line has completed.
    /// </summary>
    internal HttpResponseHeader PendingHeader => ToHeader(pendingHeader.ToString());

    /// <summary>
    /// Starts the next response's head with its status line, dropping the previous head's
    /// headers but keeping its bytes.
    /// </summary>
    /// <param name="statusLine">The status line.</param>
    /// <exception cref="HttpTransferException">The heads grew past <see cref="MaximumHeadSize" /> (exit 56).</exception>
    internal void StartHead(HttpLine statusLine)
    {
        headers.Clear();
        Append(statusLine.Content, statusLine.Terminator);
    }

    /// <summary>
    /// Adds one header line, or folds a continuation line into the header before it: that
    /// header loses its trailing blanks and gains one space and the continuation without its
    /// leading blanks. The folded line is held to the same
    /// <see cref="HttpLineReader.MaximumLineLength" /> as a single line, as curl 8.21.0 holds it.
    /// </summary>
    /// <param name="line">A non-empty line after the status line.</param>
    /// <exception cref="HttpTransferException">
    /// The line holds a NUL byte (exit 8, <c>Nul byte in header</c>); or has no colon once any
    /// leading blanks of a line with no header to fold into are dropped (exit 8,
    /// <c>Header without colon</c>); or is a
    /// second <c>Location</c> header that differs from the first (exit 8,
    /// <c>Multiple Location headers</c>); or the heads grew past <see cref="MaximumHeadSize" />
    /// (exit 56); or a folded line reached <see cref="HttpLineReader.MaximumLineLength" /> (exit 100).
    /// </exception>
    internal void AddLine(HttpLine line)
    {
        if (line.Content.Contains('\0', StringComparison.Ordinal))
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.NulByteInHeader);
        }

        if (FoldsIntoPendingHeader(line))
        {
            Fold(line);
            return;
        }

        CommitPendingHeader();
        string content = line.Content.TrimStart([' ', '\t']);
        if (!content.Contains(':', StringComparison.Ordinal))
        {
            throw HeaderWithoutColon();
        }

        KeepLocation(ToHeader(content));
        pendingHeader.Clear().Append(content);
        pendingTerminator = line.Terminator;
        hasPendingHeader = true;
    }

    /// <summary>
    /// Ends the current head at its empty line.
    /// </summary>
    /// <param name="emptyLine">The empty line.</param>
    /// <exception cref="HttpTransferException">The heads grew past <see cref="MaximumHeadSize" /> (exit 56).</exception>
    internal void EndHead(HttpLine emptyLine)
    {
        CommitPendingHeader();
        Append(string.Empty, emptyLine.Terminator);
    }

    /// <summary>
    /// Ends the current head where the peer closed, keeping the headers already received.
    /// </summary>
    /// <exception cref="HttpTransferException">The heads grew past <see cref="MaximumHeadSize" /> (exit 56).</exception>
    internal void EndHeadAtClose() => CommitPendingHeader();

    /// <summary>
    /// Builds the final head.
    /// </summary>
    /// <param name="statusLine">The final response's status line.</param>
    /// <param name="bodyPrefix">The bytes read past the head.</param>
    /// <returns>The head.</returns>
    internal HttpResponseHead Build(HttpStatusLine statusLine, byte[] bodyPrefix) =>
        new(statusLine, [.. headers], Encoding.Latin1.GetBytes(headText.ToString()), bodyPrefix);

    /// <summary>
    /// Determines whether a header line folds into the header before it: it is a continuation
    /// line and a header of the current head is pending. A continuation line with no header
    /// before it is a header of its own, its leading blanks dropped, as curl 8.21.0's
    /// <c>Curl_headers_push</c> takes it rather than failing (measured, upstream test1473, BL-1805).
    /// </summary>
    /// <param name="line">A non-empty line after the status line.</param>
    /// <returns><see langword="true" /> when the line folds into the pending header.</returns>
    internal bool FoldsIntoPendingHeader(HttpLine line) => line.IsContinuation && hasPendingHeader;

    private void Fold(HttpLine line)
    {
        while (HttpLine.IsBlank(pendingHeader[^1]))
        {
            pendingHeader.Length--;
        }

        pendingHeader.Append(' ').Append(line.Content.AsSpan().TrimStart(" \t"));
        pendingTerminator = line.Terminator;
        if (pendingHeader.Length + pendingTerminator.Length >= HttpLineReader.MaximumLineLength)
        {
            throw new HttpTransferException(CurlExitCode.TooLarge, HttpTransferMessages.LineTooLarge);
        }
    }

    /// <summary>
    /// Keeps the first non-empty <c>Location</c> value of the exchange and refuses a later one
    /// that differs from it, as curl 8.21.0's <c>http_header_l</c> does for every status code;
    /// an empty value, or an exact repeat, is ignored.
    /// </summary>
    private void KeepLocation(HttpResponseHeader header)
    {
        if (!header.Name.Equals("Location", StringComparison.OrdinalIgnoreCase) || header.Value.Length == 0)
        {
            return;
        }

        if (location is not null && !location.Equals(header.Value, StringComparison.Ordinal))
        {
            throw new HttpTransferException(CurlExitCode.WeirdServerReply, HttpTransferMessages.MultipleLocationHeaders);
        }

        location = header.Value;
    }

    private void CommitPendingHeader()
    {
        if (!hasPendingHeader)
        {
            return;
        }

        string header = pendingHeader.ToString();
        headers.Add(ToHeader(header));
        Append(header, pendingTerminator);
        hasPendingHeader = false;
    }

    private HttpResponseHeader ToHeader(string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        return new HttpResponseHeader(header[..colon], header[(colon + 1)..].Trim(' ', '\t')) { LineStart = headText.Length };
    }

    private void Append(string content, string terminator)
    {
        headText.Append(content).Append(terminator);
        if (headText.Length > MaximumHeadSize)
        {
            throw new HttpTransferException(CurlExitCode.RecvError, HttpTransferMessages.HeadTooLarge(headText.Length));
        }
    }

    private static HttpTransferException HeaderWithoutColon() =>
        new(CurlExitCode.WeirdServerReply, HttpTransferMessages.HeaderWithoutColon);
}
