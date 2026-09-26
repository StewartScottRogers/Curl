namespace Curl.Protocol.Abstractions;

/// <summary>
/// Opens local files on behalf of a protocol handler: the seam that keeps
/// <c>file://</c> off the disk during tests.
/// </summary>
/// <remarks>
/// <para>
/// This is the second transport seam alongside <see cref="IConnection" />, per
/// ADR-0002. <c>file://</c> has no wire, and every one of its observable behaviours is
/// metadata or positioning that a byte pipe cannot express: <c>Content-Length</c> and
/// <c>Last-Modified</c> come from the opened handle, <c>-r</c>/<c>--range</c> and
/// <c>-C</c>/<c>--continue-at</c> are seeks, truncate-versus-append is an open mode, and
/// curl's choice between exit 37 (<see cref="CurlExitCode.FileCouldntReadFile" />) and
/// exit 23 (<see cref="CurlExitCode.WriteError" />) turns on which direction the open
/// failed in. <see cref="IConnection.IsSecure" /> and
/// <see cref="IConnection.RemoteEndPoint" /> are meaningless for a local file, and
/// routing a file through a duplex byte pipe would mean inventing a header-and-body
/// protocol that exists nowhere upstream.
/// </para>
/// <para>
/// There is deliberately no <c>Exists</c> and no <c>Stat</c> member. curl opens the
/// file even for <c>-I</c>/<c>--head</c> — a directory with <c>-I</c> gives the same
/// exit 37 a directory without it does — so a separate existence check would be a
/// behaviour that then had to be suppressed to stay compatible. Ask for the handle and
/// read <see cref="FileOpenResult.Status" /> on the way back.
/// </para>
/// <para>
/// A handler receives this interface and constructs no <see cref="FileStream" /> of its
/// own, so its tests drive it against an in-memory implementation with no disk access
/// at all.
/// </para>
/// </remarks>
public interface IFileSystem
{
    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    /// <param name="path">
    /// An operating-system path, already percent-decoded. URL knowledge stays in the
    /// protocol handler; this interface never sees a <see cref="Uri" />.
    /// </param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>
    /// The outcome. When it succeeded, <see cref="FileOpenResult.Content" /> is
    /// seekable, so a range or resume offset is applied by seeking it.
    /// </returns>
    ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a file for writing, creating it when it does not exist.
    /// </summary>
    /// <param name="path">
    /// An operating-system path, already percent-decoded. URL knowledge stays in the
    /// protocol handler; this interface never sees a <see cref="Uri" />.
    /// </param>
    /// <param name="mode">Whether existing content is discarded or appended to.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>
    /// The outcome. When it succeeded, <see cref="FileOpenResult.Content" /> is
    /// positioned where the first write lands: the start of the file for
    /// <see cref="FileWriteMode.Truncate" />, the end of the existing content for
    /// <see cref="FileWriteMode.Append" />.
    /// </returns>
    ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        CancellationToken cancellationToken);
}
