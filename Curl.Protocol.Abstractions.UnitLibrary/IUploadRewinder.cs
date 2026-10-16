namespace Curl.Protocol.Abstractions;

/// <summary>
/// An upload stream that cannot seek but can be taken back to its start for a resend, as
/// libcurl's read callback can through <c>CURLOPT_SEEKFUNCTION</c>. A followed redirect that
/// resends the body, an authentication retry, a resend after a <c>417</c> and a retry after a
/// refused stream rewind it through <see cref="RewindToStart" /> once some of it was read
/// (<see cref="HasBeenRead" />), as curl 8.22.0's <c>cr_in_rewind</c> does; a rewind that fails
/// ends the transfer with exit 65 (<see cref="CurlExitCode.SendFailRewind" />) and the
/// rewinder's message. A seekable stream is rewound through its <see cref="Stream.Position" />
/// instead, whether or not it implements this.
/// </summary>
public interface IUploadRewinder
{
    /// <summary>
    /// The <c>-v</c> line curl 8.22.0 writes after the rewinder's own message when a rewind
    /// fails: its client reader <c>cr-in</c> and exit 65.
    /// </summary>
    const string ReaderRewindFailedLine = "rewind of client reader 'cr-in' failed: 65";

    /// <summary>
    /// Gets a value indicating whether any of the upload has been read, so a resend needs it
    /// rewound; curl rewinds nothing when the read callback was never called.
    /// </summary>
    bool HasBeenRead { get; }

    /// <summary>
    /// Takes the upload back to its start, so the next read gives its first byte again.
    /// </summary>
    /// <returns>
    /// <see langword="null" /> when the rewind succeeded; otherwise the message the transfer
    /// fails with, such as <c>seek callback returned error 1</c>.
    /// </returns>
    string? RewindToStart();
}
