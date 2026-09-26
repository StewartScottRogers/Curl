namespace Curl.Protocol.Abstractions;

/// <summary>
/// Everything a protocol handler needs for one transfer, assembled by the command
/// line layer and handed to the handler.
/// </summary>
public interface ITransferContext
{
    /// <summary>
    /// Gets the URL being transferred, after any scheme rewriting has been applied.
    /// </summary>
    Uri Url { get; }

    /// <summary>
    /// Gets the stream that received data is written to.
    /// </summary>
    Stream Output { get; }

    /// <summary>
    /// Gets the stream to upload from, or <see langword="null" /> for a download.
    /// </summary>
    Stream? Upload { get; }

    /// <summary>
    /// Gets the byte offset a transfer resumes from, per <c>-C</c>/<c>--continue-at</c>,
    /// or <see langword="null" /> when the caller is not resuming.
    /// </summary>
    /// <remarks>
    /// A download seeks the source to this offset and appends to the destination; an
    /// upload skips this many bytes of the local file. It is an offset, not a size, so
    /// zero means "resume from the start" and is not the same as
    /// <see langword="null" />.
    /// </remarks>
    long? ResumeFrom { get; }

    /// <summary>
    /// Gets the byte range requested with <c>-r</c>/<c>--range</c>, or
    /// <see langword="null" /> when the whole resource was asked for.
    /// </summary>
    /// <remarks>
    /// curl accepts a comma-separated list but honours only the first range for
    /// <c>file://</c>. The range text is for parsing once, before any handler runs, by
    /// <c>ByteRangeParser</c> in <c>Curl.Core.UnitLibrary</c>, which also answers text that
    /// names no range with exit 33 (<see cref="CurlExitCode.RangeError" />); so a handler
    /// sees at most one <see cref="Abstractions.ByteRange" />, already validated. <c>Curl.Console</c>
    /// does not call it yet (task BL-090).
    /// </remarks>
    ByteRange? Range { get; }

    /// <summary>
    /// Gets the largest body, in bytes, that <c>--max-filesize</c> allows a download to
    /// deliver, or <see langword="null" /> when no limit was given.
    /// </summary>
    /// <remarks>
    /// Zero also means no limit, as it does to curl. Measured on curl 8.21.0 over
    /// <c>file://</c>, a download with more body bytes than this writes exactly this many,
    /// then fails with exit 63 (<see cref="CurlExitCode.FilesizeExceeded" />); the limit
    /// counts body bytes only, so headers written to <see cref="HeaderOutput" /> do not use
    /// it up, and an upload ignores it. <c>file://</c> enforces it; the other handlers do not
    /// read it yet.
    /// </remarks>
    long? MaxFileSize { get; }

    /// <summary>
    /// Gets a value indicating whether only metadata was asked for, per
    /// <c>-I</c>/<c>--head</c>: pseudo-headers are written and no body is.
    /// </summary>
    /// <remarks>
    /// The resource is still opened. curl's <c>-I</c> on a directory reports the same
    /// exit 37 (<see cref="CurlExitCode.FileCouldntReadFile" />) that a body transfer
    /// would, because suppressing the body does not suppress the open.
    /// </remarks>
    bool NoBody { get; }

    /// <summary>
    /// Gets the condition from <c>-z</c>/<c>--time-cond</c> that decides whether the body
    /// is transferred at all, or <see langword="null" /> when none was given.
    /// </summary>
    TimeCondition? TimeCondition { get; }

    /// <summary>
    /// Gets the stream that headers are written to for <c>-i</c>/<c>--include</c> and
    /// <c>-D</c>/<c>--dump-header</c>, or <see langword="null" /> when the caller asked
    /// for no header output.
    /// </summary>
    /// <remarks>
    /// This may be the same stream as <see cref="Output" />, which is what <c>-i</c>
    /// means, or a separate one, which is what <c>-D</c> means. A handler writes headers
    /// here before any body and never inspects which case it has. For <c>file://</c> the
    /// headers are curl's synthesised <c>Content-Length</c>, <c>Accept-ranges</c> and
    /// <c>Last-Modified</c> lines rather than anything received from a peer.
    /// </remarks>
    Stream? HeaderOutput { get; }

    /// <summary>
    /// Gets the data given with <c>-d</c>/<c>--data</c>, or <see langword="null" /> when
    /// none was given.
    /// </summary>
    /// <remarks>
    /// <c>mqtt://</c> reads it and sends it as a PUBLISH instead of subscribing
    /// (ADR-0006). A scheme that has no use for request data ignores it.
    /// </remarks>
    ReadOnlyMemory<byte>? PostData { get; }

    /// <summary>
    /// Gets the user name and password from <c>-u</c>/<c>--user</c>, else from the URL's
    /// user information, or <see langword="null" /> when neither is present.
    /// </summary>
    /// <remarks>
    /// <c>mqtt://</c> reads it and sends it in its CONNECT packet (ADR-0006). A scheme
    /// that does not authenticate ignores it.
    /// </remarks>
    System.Net.NetworkCredential? Credentials { get; }

    /// <summary>
    /// Gets each <c>-t</c>/<c>--telnet-option</c> value verbatim, in command-line order,
    /// or an empty list when none was given.
    /// </summary>
    /// <remarks>
    /// <c>telnet://</c> reads it. The values arrive unvalidated: curl rejects an unknown
    /// option name (exit 48) or a value without <c>=</c> (exit 49) at transfer time,
    /// after connecting, so the telnet handler validates them (ADR-0006).
    /// </remarks>
    IReadOnlyList<string> TelnetOptions { get; }

    /// <summary>
    /// Gets the block size given with <c>--tftp-blksize</c>, as given and unclamped, or
    /// <see langword="null" /> when none was given.
    /// </summary>
    /// <remarks>
    /// <c>tftp://</c> reads it and clamps it to 8-65464, as curl does rather than
    /// refusing an out-of-range value; when it is <see langword="null" /> the TFTP
    /// default of 512 applies (ADR-0006).
    /// </remarks>
    int? TftpBlockSize { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--tftp-no-options</c> was given, which
    /// suppresses the RFC 2347, 2348 and 2349 options; <see langword="false" /> when not
    /// given.
    /// </summary>
    /// <remarks>
    /// <c>tftp://</c> reads it (ADR-0006).
    /// </remarks>
    bool TftpNoOptions { get; }

    /// <summary>
    /// Gets a value indicating whether <c>--crlf</c> was given, which converts each line
    /// feed in an upload to a carriage return plus line feed; <see langword="false" /> when
    /// not given.
    /// </summary>
    /// <remarks>
    /// It applies to uploads only; a download ignores it. <c>file://</c> reads it, and
    /// measured on curl 8.21.0 the conversion inserts a carriage return before a line feed
    /// only when the byte before that line feed is not already one, so <c>a\r\nb</c> is
    /// sent unchanged, a lone carriage return is left alone, and that state carries across
    /// chunk boundaries. The upload's byte count is the converted count (ADR-0003).
    /// </remarks>
    bool ConvertLineEndings { get; }

    /// <summary>
    /// Gets the permission bits a file created by an upload receives on a POSIX system,
    /// per <c>--create-file-mode</c>; curl's default of <c>0644</c> when not given.
    /// </summary>
    /// <remarks>
    /// Upstream curl applies it to files created remotely by an upload, over
    /// <c>file://</c>, SFTP and SCP; it does not apply to <c>-o</c>/<c>--output</c>.
    /// <c>file://</c> passes it to <see cref="IFileSystem.OpenForWriteAsync" />, where the
    /// process umask still applies, a file that already exists keeps its mode, and
    /// Windows ignores it.
    /// </remarks>
    UnixFileMode CreateFileMode { get; }

    /// <summary>
    /// Gets the time source. Injected so that timeout and retry behaviour is testable
    /// without a real delay.
    /// </summary>
    TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the token that cancels this transfer.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
