using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory file system: reads serve the path's <see cref="ExistingContent" /> entry, or
/// <see cref="ReadContent" /> for any other path, and fail with <c>NotFound</c> for a path in
/// <see cref="UnreadablePaths" />; writes land in <see cref="Written" />, appending to the
/// path's <see cref="ExistingContent" /> under <see cref="FileWriteMode.Append" />, and a path
/// in <see cref="UnwritablePaths" /> fails to open for writing with <see cref="UnwritableStatus" />, and so does a
/// path in <see cref="UntruncatablePaths" /> opened with <see cref="FileWriteMode.Truncate" />.
/// Each last-write time set is recorded in <see cref="LastWriteTimesSet" />, with whether the
/// path's written stream was still open at the time; setting one fails with
/// <see cref="FileTimeErrorCode" /> when that is not zero. A <see cref="FileWriteMode.CreateNew" />
/// open fails with <c>AlreadyExists</c> for a path in <see cref="ExistingPaths" /> (kept apart from
/// <see cref="ExistingContent" />, which only feeds reads); a path added to it by
/// <see cref="BeforeCreateNew" /> just before the open counts too. As <see cref="IOutputPaths" />,
/// every directory is created, into
/// <see cref="CreatedDirectories" />, except those in <see cref="UncreatableDirectories" />, which fail
/// with <c>EINVAL</c>, and those in <see cref="DirectoryErrorNumbers" />, which fail with their errno; a
/// path exists when it is in <see cref="ExistingPaths" />. Removing a file records the path in
/// <see cref="DeleteAttempts" />; a path in <see cref="NonRegularPaths" /> is not a regular file,
/// one in <see cref="UndeletablePaths" /> fails, and any other is dropped from
/// <see cref="Written" /> and <see cref="ExistingPaths" />, removed when it had been written.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem, IFileTimeSetter, IOutputPaths
{
    public HashSet<string> ExistingPaths { get; } = [];

    public Action<string>? BeforeCreateNew { get; set; }

    public HashSet<string> UncreatableDirectories { get; } = [];

    public Dictionary<string, int> DirectoryErrorNumbers { get; } = [];

    public List<string> CreatedDirectories { get; } = [];

    public HashSet<string> UndeletablePaths { get; } = [];

    public HashSet<string> NonRegularPaths { get; } = [];

    public List<string> DeleteAttempts { get; } = [];

    public byte[] ReadContent { get; init; } = [];

    public Dictionary<string, byte[]> ExistingContent { get; } = [];

    public HashSet<string> UnreadablePaths { get; } = [];

    public HashSet<string> UnwritablePaths { get; } = [];

    public HashSet<string> WriteFailingPaths { get; } = [];

    public HashSet<string> UntruncatablePaths { get; } = [];

    public FileAccessStatus UnwritableStatus { get; init; } = FileAccessStatus.NotFound;

    public Dictionary<string, MemoryStream> Written { get; } = [];

    public List<UnixFileMode> CreateModes { get; } = [];

    public List<FileWriteMode> WriteModes { get; } = [];

    public List<(string Path, long LastWriteUnixSeconds, bool WhileOpen)> LastWriteTimesSet { get; } = [];

    public int FileTimeErrorCode { get; init; }

    public List<string> ReadPaths { get; } = [];

    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
    {
        ReadPaths.Add(path);
        if (UnreadablePaths.Contains(path))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        byte[] content = ExistingContent.GetValueOrDefault(path, ReadContent);

        return ValueTask.FromResult(FileOpenResult.Opened(new MemoryStream(content, writable: false), content.Length, null));
    }

    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        CancellationToken cancellationToken)
    {
        CreateModes.Add(createMode);
        WriteModes.Add(mode);

        if (UnwritablePaths.Contains(path) || (mode == FileWriteMode.Truncate && UntruncatablePaths.Contains(path)))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(UnwritableStatus));
        }

        if (mode == FileWriteMode.CreateNew)
        {
            BeforeCreateNew?.Invoke(path);
        }

        if (mode == FileWriteMode.CreateNew && ExistingPaths.Contains(path))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.AlreadyExists));
        }

        MemoryStream stream = WriteFailingPaths.Contains(path) ? new FailingWriteStream() : new MemoryStream();
        if (mode == FileWriteMode.Append && ExistingContent.TryGetValue(path, out byte[]? existing))
        {
            stream.Write(existing);
        }

        Written[path] = stream;

        return ValueTask.FromResult(FileOpenResult.Opened(stream, 0, null));
    }

    public bool TrySetLastWriteUnixSeconds(string path, long unixSeconds, out int errorCode)
    {
        bool whileOpen = Written.TryGetValue(path, out MemoryStream? stream) && stream.CanWrite;
        LastWriteTimesSet.Add((path, unixSeconds, whileOpen));
        errorCode = FileTimeErrorCode;

        return errorCode == 0;
    }

    public bool TryCreateDirectory(string path, out int errorNumber)
    {
        if (DirectoryErrorNumbers.TryGetValue(path, out errorNumber))
        {
            return false;
        }

        errorNumber = UncreatableDirectories.Contains(path) ? 22 : 0;
        if (errorNumber != 0)
        {
            return false;
        }

        CreatedDirectories.Add(path);

        return true;
    }

    public bool Exists(string path) => ExistingPaths.Contains(path);

    public OutputFileRemoval RemoveFile(string path)
    {
        DeleteAttempts.Add(path);
        if (NonRegularPaths.Contains(path))
        {
            return OutputFileRemoval.NotRegularFile;
        }

        if (UndeletablePaths.Contains(path))
        {
            return OutputFileRemoval.Failed;
        }

        ExistingPaths.Remove(path);

        return Written.Remove(path) ? OutputFileRemoval.Removed : OutputFileRemoval.Failed;
    }
}
