using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File.Fakes;

/// <summary>
/// One recorded call to <see cref="FakeFileSystem" />.
/// </summary>
/// <param name="Kind">Which member was called.</param>
/// <param name="Path">The operating-system path the caller asked for.</param>
/// <param name="WriteMode">
/// The write mode for an open-for-write, or <see langword="null" /> for an
/// open-for-read.
/// </param>
/// <param name="CreateMode">
/// The mode a created file would receive, for an open-for-write, or
/// <see langword="null" /> for an open-for-read.
/// </param>
/// <remarks>
/// The ordered list of these is the <c>file</c> scheme's equivalent of the bytes an FTP
/// handler puts on the wire: it is the whole of what the handler did, observable without
/// a disk.
/// </remarks>
public sealed record FileSystemCall(FileSystemCallKind Kind, string Path, FileWriteMode? WriteMode, UnixFileMode? CreateMode)
{
    /// <summary>
    /// Creates the record of an open for reading.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <returns>The expected call.</returns>
    public static FileSystemCall Read(string path) =>
        new(FileSystemCallKind.OpenForRead, path, null, null);

    /// <summary>
    /// Creates the record of an open for writing.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="mode">The write mode asked for.</param>
    /// <param name="createMode">
    /// The mode asked for a created file; curl's default of <c>0644</c> when omitted.
    /// </param>
    /// <returns>The expected call.</returns>
    public static FileSystemCall Write(
        string path,
        FileWriteMode mode,
        UnixFileMode createMode = TransferContext.DefaultCreateFileMode) =>
        new(FileSystemCallKind.OpenForWrite, path, mode, createMode);
}
