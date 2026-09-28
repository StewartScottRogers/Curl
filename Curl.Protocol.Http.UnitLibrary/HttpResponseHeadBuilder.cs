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

    /// <summary>
    /// Gets the header added last to the current head, its continuation lines folded in; valid
    /// once <see cref="AddLine" />, <see cref="EndHead" /> or <see cref="EndHeadAtClose" /> has
    /// completed a header of it.
    /// </summary>
    internal HttpResponseHeader LastHeader => headers[^1];

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
    /// The line has no colon, or continues a header that is not there (exit 8,
    /// <c>Header without colon</c>); or the heads grew past <see cref="MaximumHeadSize" />
    /// (exit 56); or a folded line reached <see cref="HttpLineReader.MaximumLineLength" /> (exit 100).
    /// </exception>
    internal void AddLine(HttpLine line)
    {
        if (line.IsContinuation)
        {
            Fold(line);
            return;
        }

        CommitPendingHeader();
        if (!line.Content.Contains(':', StringComparison.Ordinal))
        {
            throw HeaderWithoutColon();
        }

        pendingHeader.Clear().Append(line.Content);
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

    private void Fold(HttpLine line)
    {
        if (!hasPendingHeader)
        {
            throw HeaderWithoutColon();
        }

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

    private void CommitPendingHeader()
    {
        if (!hasPendingHeader)
        {
            return;
        }

        string header = pendingHeader.ToString();
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        headers.Add(new HttpResponseHeader(header[..colon], header[(colon + 1)..].Trim(' ', '\t')) { LineStart = headText.Length });
        Append(header, pendingTerminator);
        hasPendingHeader = false;
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
