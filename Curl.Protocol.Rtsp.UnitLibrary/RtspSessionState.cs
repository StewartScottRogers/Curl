using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// The <c>CSeq</c> counter and session ID one RTSP transfer carries from request to request
/// (ADR-0169 decision 4).
/// </summary>
/// <remarks>
/// A reply's <c>Session</c> ID is read as curl 8.21.0 reads it (measured, BL-592): the header
/// name in any case, then blanks and tabs skipped, then every byte above a blank and below 0x80
/// up to the first <c>;</c>. The first ID a transfer sees is kept, even an empty one
/// (<c>Session:</c> or <c>Session: ;x</c>); a later <c>Session</c> header naming another ID,
/// in the same reply or a later one, fails with 86,
/// <c>Got RTSP Session ID Line [&lt;rest of the line&gt;], but wanted ID [&lt;kept ID&gt;]</c>,
/// where the rest of the line starts at the ID and keeps its line ending.
/// </remarks>
/// <param name="firstSequenceNumber">The <c>CSeq</c> of the transfer's first request.</param>
internal sealed class RtspSessionState(long firstSequenceNumber)
{
    /// <summary>Gets the <c>CSeq</c> the next request is sent with.</summary>
    internal long NextSequenceNumber { get; private set; } = firstSequenceNumber;

    /// <summary>
    /// Gets the session ID the server gave, sent on every later request, or
    /// <see langword="null" /> before a reply has carried one.
    /// </summary>
    internal string? SessionId { get; private set; }

    /// <summary>Takes the <c>CSeq</c> for a request and moves the counter on by one.</summary>
    /// <returns>The <c>CSeq</c> to send.</returns>
    internal long TakeSequenceNumber() => NextSequenceNumber++;

    /// <summary>Reads the ID from a reply's <c>Session</c> header line, keeping or checking it.</summary>
    /// <param name="value">The line after <c>Session:</c>, with its line ending.</param>
    /// <exception cref="RtspTransferException">A different ID is already kept: exit 86.</exception>
    internal void AcceptSession(string value)
    {
        string rest = value.TrimStart(' ', '\t');
        int end = rest.AsSpan().IndexOfAnyExceptInRange('!', '\u007f');
        string id = rest[..end].Split(';')[0];
        if (SessionId is null)
        {
            SessionId = id;
        }
        else if (SessionId != id)
        {
            throw new RtspTransferException(
                CurlExitCode.RtspSessionError,
                $"Got RTSP Session ID Line [{rest}], but wanted ID [{SessionId}]");
        }
    }
}
