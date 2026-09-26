using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An in-memory file system: reads serve <see cref="ReadContent" /> for any path, writes
/// land in <see cref="Written" />, and a path in <see cref="UnwritablePaths" /> fails to open
/// for writing with <see cref="UnwritableStatus" />.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    public byte[] ReadContent { get; init; } = [];

    public HashSet<string> UnwritablePaths { get; } = [];

    public FileAccessStatus UnwritableStatus { get; init; } = FileAccessStatus.NotFound;

    public Dictionary<string, MemoryStream> Written { get; } = [];

    public List<UnixFileMode> CreateModes { get; } = [];

    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(FileOpenResult.Opened(new MemoryStream(ReadContent, writable: false), ReadContent.Length, null));

    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        CancellationToken cancellationToken)
    {
        CreateModes.Add(createMode);

        if (UnwritablePaths.Contains(path))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(UnwritableStatus));
        }

        MemoryStream stream = new();
        Written[path] = stream;

        return ValueTask.FromResult(FileOpenResult.Opened(stream, 0, null));
    }
}
