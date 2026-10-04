using System.Net;

namespace Curl.Protocol.Tftp;

/// <summary>
/// What a transfer passes on when the server's first reply switches its direction, as
/// curl 8.21.0's <c>tftp_send_first</c> does (connecting for transmit on an ACK and for
/// receive on DATA, whatever the request asked): a download handing over to an upload
/// of nothing, or an upload to a download (BL-1444).
/// </summary>
/// <param name="Request">The request sent, which is the last packet until another is sent.</param>
/// <param name="Retries">The retries counted so far.</param>
/// <param name="ResendAt">When the next re-send is due, as time elapsed since the transfer started.</param>
/// <param name="NotedFailure">The first failure message curl noted, kept as the message of whatever failure ends the transfer.</param>
/// <param name="Reply">The first reply's bytes, which the new direction answers first.</param>
/// <param name="Source">Where the first reply came from.</param>
internal sealed record TftpHandOver(
    byte[] Request,
    int Retries,
    TimeSpan ResendAt,
    string? NotedFailure,
    byte[] Reply,
    EndPoint Source);
