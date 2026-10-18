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
/// <para>
/// A libcurl caller keeps one state on its easy handle and hands it to every perform, so a
/// second perform continues the <c>CSeq</c> and sends the session ID an earlier reply gave, as
/// libcurl's <c>data-&gt;state.rtsp_next_client_CSeq</c> and
/// <c>data-&gt;set.str[STRING_RTSP_SESSION_ID]</c> do (BL-2751). A new state's counter is 0, and
/// a perform on a new connection moves a 0 to 1, as <c>rtsp_connect</c> does; one on a reused
/// connection keeps it.
/// </para>
/// </remarks>
public sealed class RtspSessionState
{
    /// <summary>Initializes a new instance of the <see cref="RtspSessionState" /> class for a new easy handle.</summary>
    public RtspSessionState()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RtspSessionState" /> class.</summary>
    /// <param name="firstSequenceNumber">The <c>CSeq</c> of the transfer's first request.</param>
    internal RtspSessionState(long firstSequenceNumber) => NextSequenceNumber = firstSequenceNumber;

    /// <summary>Gets the <c>CSeq</c> the next request is sent with.</summary>
    internal long NextSequenceNumber { get; private set; }

    /// <summary>
    /// Gets the session ID the server gave, sent on every later request, or
    /// <see langword="null" /> before a reply has carried one.
    /// </summary>
    internal string? SessionId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a reply has made curl close the connection after the
    /// transfer instead of keeping it, as a <c>Content-Length</c> too large for 64 bits does
    /// (curl's <c>streamclose</c>, measured, BL-1403).
    /// </summary>
    internal bool ClosesConnection { get; private set; }

    /// <summary>
    /// Readies the state for a perform: the connection is kept unless a reply says otherwise, a
    /// set client <c>CSeq</c> and a set session ID take effect, and on a new connection a
    /// <c>CSeq</c> of 0 becomes 1.
    /// </summary>
    /// <param name="settings">The perform's libcurl request settings, or <see langword="null" /> for the tool's transfer.</param>
    /// <param name="connectionReused">Whether the perform runs on a reused connection.</param>
    internal void StartTransfer(RtspRequestSettings? settings, bool connectionReused)
    {
        ClosesConnection = false;
        if (settings is not null)
        {
            ApplySettings(settings);
        }

        if (!connectionReused && NextSequenceNumber == 0)
        {
            NextSequenceNumber = RtspProtocolHandler.FirstSequenceNumber;
        }
    }

    private void ApplySettings(RtspRequestSettings settings)
    {
        NextSequenceNumber = settings.ClientSequenceNumber ?? NextSequenceNumber;
        SessionId = settings.SessionId ?? SessionId;
    }

    /// <summary>Marks the connection to be closed, not kept, once the transfer ends.</summary>
    internal void CloseConnection() => ClosesConnection = true;

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
