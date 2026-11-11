using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Http;

/// <summary>
/// Shows each request head and response head line as curl does for http (ADR-1238): the request head goes to
/// <see cref="ITransferEvents.ReportRequestHeader"/>, so <c>-v</c> prints it as <c>&gt;</c> lines, and each response line,
/// the status line and the final empty line included, goes to <see cref="ITransferEvents.ReportResponseHeader"/>, so
/// <c>-v</c> prints it as a <c>&lt;</c> line, and to the header output of <c>-i</c> and <c>-D</c>.
/// </summary>
/// <param name="events">Where the heads are reported for <c>-v</c> and <c>--trace</c>.</param>
/// <param name="headerOutput">The <c>-i</c> or <c>-D</c> stream, or <see langword="null"/> for neither.</param>
public sealed class S3HttpHeadReporter(ITransferEvents events, Stream? headerOutput)
{
    /// <summary>Gets a reporter that shows nothing.</summary>
    public static S3HttpHeadReporter None { get; } = new(NoTransferEvents.Instance, null);

    /// <summary>Reports a request head exactly as written, its CRLFs and final empty line included.</summary>
    /// <param name="head">The head's bytes.</param>
    public void ReportRequestHead(ReadOnlySpan<byte> head) => events.ReportRequestHeader(head);

    /// <summary>Reports one response head line, read without its line ending, with CRLF after it.</summary>
    /// <param name="line">The line.</param>
    /// <param name="cancellationToken">Cancels the write to the header output.</param>
    /// <returns>A task that completes when the line is written.</returns>
    public async ValueTask ReportResponseLineAsync(string line, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(line + "\r\n");
        events.ReportResponseHeader(bytes);
        if (headerOutput is not null)
        {
            await headerOutput.WriteAsync(bytes, cancellationToken);
        }
    }
}
