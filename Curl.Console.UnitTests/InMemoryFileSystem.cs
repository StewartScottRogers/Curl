using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory file system: reads serve the path's <see cref="ExistingContent" /> entry, or
/// <see cref="ReadContent" /> for any other path, and fail with <c>NotFound</c> for a path in
/// <see cref="UnreadablePaths" />; writes land in <see cref="Written" />, appending to the
/// path's <see cref="ExistingContent" /> under <see cref="FileWriteMode.Append" />, and a path
/// in <see cref="UnwritablePaths" /> fails to open for writing with <see cref="UnwritableStatus" />.
/// Each last-write time set is recorded in <see cref="LastWriteTimesSet" />, with whether the
/// path's written stream was still open at the time; setting one fails with
/// <see cref="FileTimeErrorCode" /> when that is not zero. As <see cref="IOutputPaths" />, a
/// file exists when its path is in <see cref="ExistingPaths" /> (kept apart from
/// <see cref="ExistingContent" />, which only feeds reads), and every directory is created, into
/// <see cref="CreatedDirectories" />, except those in <see cref="UncreatableDirectories" />.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem, IFileTimeSetter, IOutputPaths
{
    public HashSet<string> ExistingPaths { get; } = [];

    public HashSet<string> UncreatableDirectories { get; } = [];

    public List<string> CreatedDirectories { get; } = [];

    public byte[] ReadContent { get; init; } = [];

    public Dictionary<string, byte[]> ExistingContent { get; } = [];

    public HashSet<string> UnreadablePaths { get; } = [];

    public HashSet<string> UnwritablePaths { get; } = [];

    public FileAccessStatus UnwritableStatus { get; init; } = FileAccessStatus.NotFound;

    public Dictionary<string, MemoryStream> Written { get; } = [];

    public List<UnixFileMode> CreateModes { get; } = [];

    public List<FileWriteMode> WriteModes { get; } = [];

    public List<(string Path, DateTimeOffset LastWriteTimeUtc, bool WhileOpen)> LastWriteTimesSet { get; } = [];

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

        if (UnwritablePaths.Contains(path))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(UnwritableStatus));
        }

        MemoryStream stream = new();
        if (mode == FileWriteMode.Append && ExistingContent.TryGetValue(path, out byte[]? existing))
        {
            stream.Write(existing);
        }

        Written[path] = stream;

        return ValueTask.FromResult(FileOpenResult.Opened(stream, 0, null));
    }

    public bool TrySetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc, out int errorCode)
    {
        bool whileOpen = Written.TryGetValue(path, out MemoryStream? stream) && stream.CanWrite;
        LastWriteTimesSet.Add((path, lastWriteTimeUtc, whileOpen));
        errorCode = FileTimeErrorCode;

        return errorCode == 0;
    }

    public bool FileExists(string path) => ExistingPaths.Contains(path);

    public bool TryCreateDirectory(string path)
    {
        if (UncreatableDirectories.Contains(path))
        {
            return false;
        }

        CreatedDirectories.Add(path);

        return true;
    }
}
