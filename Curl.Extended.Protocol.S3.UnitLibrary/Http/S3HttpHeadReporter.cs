using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Http;

/// <summary>
/// Shows each request head and response head line as curl does for http (ADR-1244): the request head goes to
/// <see cref="ITransferEvents.ReportRequestHeader"/>, so <c>-v</c> prints it as <c>&gt;</c> lines, and each response line,
/// the status line and the final empty line included, goes to <see cref="ITransferEvents.ReportResponseHeader"/>, so
/// <c>-v</c> prints it as a <c>&lt;</c> line, and to the header output of <c>-i</c> and <c>-D</c>. It also remembers the
/// last response's status, version and content type and the heads' size for <c>-w</c> (ADR-1260).
/// </summary>
/// <param name="events">Where the heads are reported for <c>-v</c> and <c>--trace</c>.</param>
/// <param name="headerOutput">The <c>-i</c> or <c>-D</c> stream, or <see langword="null"/> for neither.</param>
public sealed class S3HttpHeadReporter(ITransferEvents events, Stream? headerOutput)
{
    private const string ContentTypePrefix = "Content-Type:";

    private int responseCode;
    private Version? httpVersion;
    private string? contentType;
    private long headerSize;
    private bool expectsStatusLine;

    /// <summary>Gets a reporter that shows nothing.</summary>
    public static S3HttpHeadReporter None => new(NoTransferEvents.Instance, null);

    /// <summary>
    /// Gets what <c>-w</c> reports about the exchange so far: the last response's status code, HTTP version and
    /// <c>Content-Type</c>, and the size of every response head read; <see langword="null"/> before any response head.
    /// </summary>
    public TransferReport? Report => httpVersion is null
        ? null
        : new TransferReport { ResponseCode = responseCode, HttpVersion = httpVersion, ContentType = contentType, HeaderSize = headerSize };

    /// <summary>Reports a request head exactly as written, its CRLFs and final empty line included.</summary>
    /// <param name="head">The head's bytes.</param>
    public void ReportRequestHead(ReadOnlySpan<byte> head)
    {
        expectsStatusLine = true;
        events.ReportRequestHeader(head);
    }

    /// <summary>Reports one response head line, read without its line ending, with CRLF after it.</summary>
    /// <param name="line">The line.</param>
    /// <param name="cancellationToken">Cancels the write to the header output.</param>
    /// <returns>A task that completes when the line is written.</returns>
    public async ValueTask ReportResponseLineAsync(string line, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(line + "\r\n");
        Remember(line, bytes.Length);
        events.ReportResponseHeader(bytes);
        if (headerOutput is not null)
        {
            await headerOutput.WriteAsync(bytes, cancellationToken);
        }
    }

    private void Remember(string line, int length)
    {
        headerSize += length;
        if (expectsStatusLine)
        {
            expectsStatusLine = false;
            string[] parts = line.Split(' ', 3);
            httpVersion = parts[0] == "HTTP/1.0" ? new Version(1, 0) : new Version(1, 1);
            responseCode = parts.Length > 1 && int.TryParse(parts[1], out int code) ? code : 0;
            contentType = null;
        }
        else if (line.StartsWith(ContentTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            contentType = line[ContentTypePrefix.Length..].Trim();
        }
    }
}
