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
    /// <c>file://</c>; the command-line layer reduces the list, so a handler sees at most
    /// one <see cref="Abstractions.ByteRange" />.
    /// </remarks>
    ByteRange? Range { get; }

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
    /// Gets the time source. Injected so that timeout and retry behaviour is testable
    /// without a real delay.
    /// </summary>
    TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the token that cancels this transfer.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
