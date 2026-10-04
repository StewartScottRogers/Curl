namespace Curl.Protocol.Abstractions;

/// <summary>
/// Lists the entry names of a local directory on behalf of a protocol handler: the seam
/// that lets <c>file://</c> list a directory, as curl's Linux and macOS builds do, without
/// touching the disk during tests.
/// </summary>
/// <remarks>
/// <para>
/// curl 8.21.0's <c>lib/file.c</c> (lines 568-589, <c>file_do</c> under
/// <c>HAVE_OPENDIR</c>) lists a directory with <c>opendir</c>/<c>readdir</c>, writing each
/// entry name that does not start with <c>.</c> followed by <c>\n</c>, in <c>readdir</c>
/// order. This interface gives every name, those starting with <c>.</c> included, so the
/// handler applies curl's filter itself.
/// </para>
/// <para>
/// It is separate from <see cref="IFileSystem" /> so that the implementations of that
/// interface which cannot list need not change: a handler holding an
/// <see cref="IFileSystem" /> tests it with <c>fileSystem as IDirectoryLister</c>.
/// </para>
/// </remarks>
public interface IDirectoryLister
{
    /// <summary>
    /// Lists the names of the entries in a directory.
    /// </summary>
    /// <param name="path">
    /// An operating-system path, already percent-decoded. URL knowledge stays in the
    /// protocol handler; this interface never sees a <see cref="CurlUrl" />.
    /// </param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>
    /// The name of every entry - files, subdirectories and names starting with
    /// <c>.</c> alike, without the directory's path - in the order the operating system
    /// returns them, unsorted; or <see langword="null" /> when <paramref name="path" />
    /// cannot be listed because it is missing, is not a directory, or cannot be read.
    /// </returns>
    ValueTask<IReadOnlyList<string>?> ListEntryNamesAsync(string path, CancellationToken cancellationToken);
}
