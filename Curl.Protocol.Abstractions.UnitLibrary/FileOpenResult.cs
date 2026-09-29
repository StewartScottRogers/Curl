namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one <see cref="IFileSystem" /> open: the opened stream, and the
/// metadata that came with it.
/// </summary>
/// <param name="Status">Why the open succeeded or failed.</param>
/// <param name="Content">
/// The opened stream, non-<see langword="null" /> exactly when <see cref="IsOpen" /> is
/// <see langword="true" /> and <see langword="null" /> in every other case. Ownership
/// passes to the caller, which disposes it. For an open for reading of a regular file,
/// <see cref="Stream.CanSeek" /> is <see langword="true" />; for a character device or a
/// FIFO (<c>NUL</c>, <c>/dev/stdin</c>) it may be <see langword="false" />, with a
/// <see cref="Length" /> of zero.
/// </param>
/// <param name="Length">
/// The length in bytes of the opened handle, or zero when the open failed.
/// </param>
/// <param name="LastWriteTimeUtc">
/// The last-write timestamp of the opened handle, in Coordinated Universal Time, or
/// <see langword="null" /> when the implementation could not determine a modification
/// time — which is always the case when the open failed. <see langword="null" /> means
/// "unknown", not "the epoch": a <c>-z</c>/<c>--time-cond</c> condition that cannot be
/// evaluated transfers the body, and the <c>file://</c> header block leaves out its
/// <c>Last-Modified</c> line.
/// </param>
/// <remarks>
/// <para>
/// <see cref="Length" /> and <see cref="LastWriteTimeUtc" /> describe the handle in
/// <see cref="Content" />, not the path that was asked for. That is curl's
/// open-then-stat order, and it is why the values ride on this result instead of on a
/// separate metadata call: a file replaced between a stat and an open cannot produce a
/// <c>Content-Length</c> that disagrees with the bytes written, and
/// <c>-R</c>/<c>--remote-time</c> and <c>-z</c>/<c>--time-cond</c> cannot see a
/// timestamp belonging to a file that is no longer the one being read.
/// </para>
/// <para>
/// A read open of a regular file is seekable, so <c>-r</c>/<c>--range</c> and
/// <c>-C</c>/<c>--continue-at</c> are served by seeking <see cref="Content" />, which is
/// why <see cref="IFileSystem" /> needs no seek or position member of its own.
/// </para>
/// <para>
/// A read open of a character device or a FIFO may not be: <see cref="Stream.CanSeek" />
/// is <see langword="false" /> and <see cref="Length" /> is zero, as curl's
/// <c>fstat</c> of one reports size zero. A caller must check
/// <see cref="Stream.CanSeek" /> before seeking; the <c>file</c> handler answers a
/// download's non-zero offset on such a source with exit 36
/// (<see cref="CurlExitCode.BadDownloadResume" />), as curl 8.21.0 does for a failed
/// <c>lseek</c>.
/// </para>
/// </remarks>
public sealed record FileOpenResult(
    FileAccessStatus Status,
    Stream? Content,
    long Length,
    DateTimeOffset? LastWriteTimeUtc)
{
    /// <summary>
    /// Gets a value indicating whether the open succeeded, and therefore whether
    /// <see cref="Content" /> is non-<see langword="null" />.
    /// </summary>
    public bool IsOpen => Status == FileAccessStatus.Ok;

    /// <summary>
    /// Gets the exception the operating system raised for a failed open, when the
    /// implementation had one: what the <c>file</c> component's <c>error</c> line in the
    /// diagnostic log names (ADR-0222). <see langword="null" /> for a successful open and
    /// for a failure an implementation decided without one.
    /// </summary>
    public Exception? FailureException { get; init; }

    /// <summary>
    /// Creates the result of a successful open.
    /// </summary>
    /// <param name="content">
    /// The opened stream. A read open of a regular file is seekable; one of a character
    /// device or a FIFO may not be.
    /// </param>
    /// <param name="length">The length in bytes of the opened handle.</param>
    /// <param name="lastWriteTimeUtc">
    /// The last-write timestamp of the opened handle, in Coordinated Universal Time, or
    /// <see langword="null" /> when the implementation could not determine a modification
    /// time. <see langword="null" /> means "unknown", not "the epoch".
    /// </param>
    /// <returns>
    /// A result whose <see cref="Status" /> is <see cref="FileAccessStatus.Ok" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content" /> is <see langword="null" />, which would leave a result
    /// claiming to be open with nothing to read or write.
    /// </exception>
    public static FileOpenResult Opened(Stream content, long length, DateTimeOffset? lastWriteTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FileOpenResult(FileAccessStatus.Ok, content, length, lastWriteTimeUtc);
    }

    /// <summary>
    /// Creates the result of a failed open.
    /// </summary>
    /// <param name="status">Why the open failed.</param>
    /// <param name="failureException">
    /// The exception the operating system raised for the open, or <see langword="null" />
    /// when there was none; carried as <see cref="FailureException" />.
    /// </param>
    /// <returns>
    /// A result with no <see cref="Content" />, a <see cref="Length" /> of zero and a
    /// <see langword="null" /> <see cref="LastWriteTimeUtc" />.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="status" /> is <see cref="FileAccessStatus.Ok" />, which is not a
    /// failure; use <see cref="Opened(Stream, long, DateTimeOffset?)" /> instead.
    /// </exception>
    public static FileOpenResult Failed(FileAccessStatus status, Exception? failureException = null)
    {
        if (status == FileAccessStatus.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "A failed open cannot report FileAccessStatus.Ok; use FileOpenResult.Opened instead.");
        }

        return new FileOpenResult(status, null, 0, null) { FailureException = failureException };
    }
}
