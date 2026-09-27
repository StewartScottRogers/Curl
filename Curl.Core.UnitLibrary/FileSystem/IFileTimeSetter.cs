namespace Curl.Core.FileSystem;

/// <summary>
/// Sets the last-write time of a local file, which is how <c>-R</c>/<c>--remote-time</c>
/// stamps the <c>-o</c> output file with the source's modification time.
/// </summary>
/// <remarks>
/// It lives in Core, beside <see cref="PhysicalFileSystem" />, rather than on
/// <c>IFileSystem</c>, because only the owner of the output file sets its time, and a
/// protocol handler never owns it (ADR-0003, amendment of 2026-09-26).
/// </remarks>
public interface IFileTimeSetter
{
    /// <summary>
    /// Sets the last-write time of the file at <paramref name="path" />.
    /// </summary>
    /// <param name="path">The operating-system path of an existing file.</param>
    /// <param name="lastWriteTimeUtc">The time to set.</param>
    /// <param name="errorCode">
    /// Zero when the time was set; otherwise the Win32 error code of the failure, such as
    /// <c>2</c> (<c>ERROR_FILE_NOT_FOUND</c>) for a missing file, which curl 8.21.0 prints in
    /// its <c>Warning: GetLastError 0x%08x</c> line.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the time was set; <see langword="false" /> when it could
    /// not be, for example because the file does not exist.
    /// </returns>
    bool TrySetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc, out int errorCode);
}
