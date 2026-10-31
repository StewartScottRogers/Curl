using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Ssh.Fakes;

/// <summary>
/// An <see cref="IFileSystem" /> holding files in memory by exact path that can also be
/// written: each write's bytes land in <see cref="Written" /> when its stream is closed. It
/// can refuse every open for writing, or open one whose writes fail.
/// </summary>
/// <param name="files">The files' texts by path, for reading.</param>
internal sealed class WritableInMemoryFileSystem(IReadOnlyDictionary<string, string> files) : IFileSystem
{
    private readonly Dictionary<string, byte[]> written = [];

    /// <summary>Gets the bytes written to each path, by the last write that closed.</summary>
    public IReadOnlyDictionary<string, byte[]> Written => written;

    /// <summary>Gets the path, mode and create mode of each open for writing, in order.</summary>
    public List<(string Path, FileWriteMode Mode, UnixFileMode CreateMode)> WriteOpens { get; } = [];

    /// <summary>Gets a value indicating whether every open for writing fails with <see cref="FileAccessStatus.AccessDenied" />.</summary>
    public bool RefusesWrites { get; init; }

    /// <summary>Gets a value indicating whether a stream opened for writing throws <see cref="IOException" /> on its first write.</summary>
    public bool FailsWrites { get; init; }

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(files.TryGetValue(path, out string? text)
            ? FileOpenResult.Opened(new MemoryStream(Encoding.UTF8.GetBytes(text)), text.Length, null)
            : FileOpenResult.Failed(FileAccessStatus.NotFound));

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken)
    {
        WriteOpens.Add((path, mode, createMode));
        return ValueTask.FromResult(RefusesWrites
            ? FileOpenResult.Failed(FileAccessStatus.AccessDenied)
            : FileOpenResult.Opened(new CapturingStream(bytes => written[path] = bytes, FailsWrites), 0, null));
    }

    private sealed class CapturingStream(Action<byte[]> closed, bool failsWrites) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            failsWrites ? throw new IOException("The disk is full.") : base.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                closed(ToArray());
            }

            base.Dispose(disposing);
        }
    }
}
