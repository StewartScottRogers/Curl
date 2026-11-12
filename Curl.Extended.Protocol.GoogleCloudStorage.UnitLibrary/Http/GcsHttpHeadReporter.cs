using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Http;

/// <summary>
/// Shows each request head and response head line as curl does for http (ADR-1244): the request head goes to
/// <see cref="ITransferEvents.ReportRequestHeader"/>, so <c>-v</c> prints it as <c>&gt;</c> lines, and each response line,
/// the status line and the final empty line included, goes to <see cref="ITransferEvents.ReportResponseHeader"/>, so
/// <c>-v</c> prints it as a <c>&lt;</c> line, and to the header output of <c>-i</c> and <c>-D</c>. It also remembers the
/// last response's status, version and content type and the heads' size for <c>-w</c> (ADR-1260).
/// </summary>
/// <param name="events">Where the heads are reported for <c>-v</c> and <c>--trace</c>.</param>
/// <param name="headerOutput">The <c>-i</c> or <c>-D</c> stream, or <see langword="null"/> for neither.</param>
public sealed class GcsHttpHeadReporter(ITransferEvents events, Stream? headerOutput)
{
    private const string ContentTypePrefix = "Content-Type:";

    private int responseCode;
    private Version? httpVersion;
    private string? contentType;
    private long headerSize;

    /// <summary>Gets a reporter that shows nothing.</summary>
    public static GcsHttpHeadReporter None => new(NoTransferEvents.Instance, null);

    /// <summary>
    /// Gets what <c>-w</c> reports about the exchange so far: the last response's status code, HTTP version and
    /// <c>Content-Type</c>, and the size of every response head read; <see langword="null"/> before any response head.
    /// </summary>
    public TransferReport? Report => ReportWith(0);

    /// <summary>Gets what <see cref="Report"/> gets, with the body bytes received added for <c>%{size_download}</c>; <see langword="null"/> before any response head.</summary>
    /// <param name="downloadSize">The body bytes received.</param>
    /// <returns>The report, or <see langword="null"/>.</returns>
    public TransferReport? ReportWith(long downloadSize) => httpVersion is null
        ? null
        : new TransferReport { ResponseCode = responseCode, HttpVersion = httpVersion, ContentType = contentType, HeaderSize = headerSize, DownloadSize = downloadSize };

    /// <summary>Reports a request head exactly as written, its CRLFs and final empty line included.</summary>
    /// <param name="head">The head's bytes.</param>
    public void ReportRequestHead(ReadOnlySpan<byte> head)
    {
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
        if (line.StartsWith("HTTP/", StringComparison.Ordinal))
        {
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
