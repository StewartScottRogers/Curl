using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="IFileSystem" /> holding key files in memory by exact path, which records
/// every path opened for reading; any other path is not found. Writing is never needed.
/// </summary>
/// <param name="files">The files' texts by path.</param>
internal sealed class InMemoryKeyFileSystem(IReadOnlyDictionary<string, string> files) : IFileSystem
{
    private readonly List<string> opened = [];

    /// <summary>
    /// Gets every path opened for reading, found or not, in order.
    /// </summary>
    public IReadOnlyList<string> Opened => opened;

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
    {
        opened.Add(path);
        return ValueTask.FromResult(files.TryGetValue(path, out string? text)
            ? FileOpenResult.Opened(new MemoryStream(Encoding.Latin1.GetBytes(text)), text.Length, null)
            : FileOpenResult.Failed(FileAccessStatus.NotFound));
    }

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Key files are only read.");
}
