namespace Curl.Protocol.Rtsp;

/// <summary>What <see cref="RtspReplyReader" /> read of a reply head.</summary>
/// <param name="StatusCode">The status code, or 0 when the status line was never read.</param>
/// <param name="SequenceNumber">The reply's <c>CSeq</c>, or 0 when none was read.</param>
/// <param name="ContentLength">The reply's <c>Content-Length</c>, or 0 when none was read.</param>
/// <param name="Length">How many head bytes were received, for <c>%{size_header}</c>.</param>
/// <param name="EndLine">
/// The blank line that ended the head, not yet reported to <c>-v</c>; or
/// <see langword="null" /> when the server closed the connection first.
/// </param>
/// <param name="Remaining">The bytes received after the head: the start of the body.</param>
internal sealed record RtspReplyHead(
    int StatusCode,
    long SequenceNumber,
    long ContentLength,
    int Length,
    byte[]? EndLine,
    byte[] Remaining);
