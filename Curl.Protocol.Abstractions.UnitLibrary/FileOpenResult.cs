namespace Curl.Protocol.Abstractions;

/// <summary>
/// The outcome of one <see cref="IFileSystem" /> open: the opened stream, and the
/// metadata that came with it.
/// </summary>
/// <param name="Status">Why the open succeeded or failed.</param>
/// <param name="Content">
/// The opened stream, non-<see langword="null" /> exactly when <see cref="IsOpen" /> is
/// <see langword="true" /> and <see langword="null" /> in every other case. Ownership
/// passes to the caller, which disposes it. For an open for reading,
/// <see cref="Stream.CanSeek" /> is contractually <see langword="true" />.
/// </param>
/// <param name="Length">
/// The length in bytes of the opened handle, or zero when the open failed.
/// </param>
/// <param name="LastWriteTimeUtc">
/// The last-write timestamp of the opened handle, in Coordinated Universal Time, or the
/// <see langword="default" /> value when the open failed.
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
/// Because a read open is always seekable, <c>-r</c>/<c>--range</c> and
/// <c>-C</c>/<c>--continue-at</c> are served by seeking <see cref="Content" />, which is
/// why <see cref="IFileSystem" /> needs no seek or position member of its own.
/// </para>
/// </remarks>
public sealed record FileOpenResult(
    FileAccessStatus Status,
    Stream? Content,
    long Length,
    DateTimeOffset LastWriteTimeUtc)
{
    /// <summary>
    /// Gets a value indicating whether the open succeeded, and therefore whether
    /// <see cref="Content" /> is non-<see langword="null" />.
    /// </summary>
    public bool IsOpen => Status == FileAccessStatus.Ok;

    /// <summary>
    /// Creates the result of a successful open.
    /// </summary>
    /// <param name="content">The opened stream; seekable when the open was for reading.</param>
    /// <param name="length">The length in bytes of the opened handle.</param>
    /// <param name="lastWriteTimeUtc">
    /// The last-write timestamp of the opened handle, in Coordinated Universal Time.
    /// </param>
    /// <returns>
    /// A result whose <see cref="Status" /> is <see cref="FileAccessStatus.Ok" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content" /> is <see langword="null" />, which would leave a result
    /// claiming to be open with nothing to read or write.
    /// </exception>
    public static FileOpenResult Opened(Stream content, long length, DateTimeOffset lastWriteTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new FileOpenResult(FileAccessStatus.Ok, content, length, lastWriteTimeUtc);
    }

    /// <summary>
    /// Creates the result of a failed open.
    /// </summary>
    /// <param name="status">Why the open failed.</param>
    /// <returns>
    /// A result with no <see cref="Content" />, a <see cref="Length" /> of zero and a
    /// <see langword="default" /> <see cref="LastWriteTimeUtc" />.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="status" /> is <see cref="FileAccessStatus.Ok" />, which is not a
    /// failure; use <see cref="Opened(Stream, long, DateTimeOffset)" /> instead.
    /// </exception>
    public static FileOpenResult Failed(FileAccessStatus status)
    {
        if (status == FileAccessStatus.Ok)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "A failed open cannot report FileAccessStatus.Ok; use FileOpenResult.Opened instead.");
        }

        return new FileOpenResult(status, null, 0, default);
    }
}
