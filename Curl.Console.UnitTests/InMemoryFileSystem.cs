using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory file system: reads serve the path's <see cref="ExistingContent" /> entry, or
/// <see cref="ReadContent" /> for any other path, and fail with <c>NotFound</c> for a path in
/// <see cref="UnreadablePaths" />; writes land in <see cref="Written" />, appending to the
/// path's <see cref="ExistingContent" /> under <see cref="FileWriteMode.Append" />, and a path
/// in <see cref="UnwritablePaths" /> fails to open for writing with <see cref="UnwritableStatus" />.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    public byte[] ReadContent { get; init; } = [];

    public Dictionary<string, byte[]> ExistingContent { get; } = [];

    public HashSet<string> UnreadablePaths { get; } = [];

    public HashSet<string> UnwritablePaths { get; } = [];

    public FileAccessStatus UnwritableStatus { get; init; } = FileAccessStatus.NotFound;

    public Dictionary<string, MemoryStream> Written { get; } = [];

    public List<UnixFileMode> CreateModes { get; } = [];

    public List<FileWriteMode> WriteModes { get; } = [];

    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
    {
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
}
