using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

/// <summary>
/// An in-memory <see cref="IFileSystem" /> for the multipart tests: files are byte arrays, a
/// path can be made to fail with a chosen <see cref="FileAccessStatus" /> or to open as a
/// stream that cannot seek, and every stream it opens is kept so a test can see whether it
/// was disposed.
/// </summary>
internal sealed class FormFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    private readonly Dictionary<string, FileAccessStatus> failures = new(StringComparer.Ordinal);

    private readonly HashSet<string> unseekable = new(StringComparer.Ordinal);

    /// <summary>Gets every stream this file system has opened, in order.</summary>
    internal List<TrackedStream> Opened { get; } = [];

    /// <summary>Gets the cancellation token of every open, in order.</summary>
    internal List<CancellationToken> OpenTokens { get; } = [];

    internal FormFileSystem WithFile(string path, byte[] content)
    {
        files[path] = content;
        return this;
    }

    internal FormFileSystem WithFile(string path, string asciiContent) =>
        WithFile(path, System.Text.Encoding.ASCII.GetBytes(asciiContent));

    internal FormFileSystem WithFailure(string path, FileAccessStatus status)
    {
        failures[path] = status;
        return this;
    }

    internal FormFileSystem WithUnseekableFile(string path, string asciiContent)
    {
        unseekable.Add(path);
        return WithFile(path, asciiContent);
    }

    public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
    {
        OpenTokens.Add(cancellationToken);
        if (failures.TryGetValue(path, out FileAccessStatus status))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(status));
        }

        byte[] content = files[path];
        TrackedStream stream = new(content, unseekable.Contains(path));
        Opened.Add(stream);
        return ValueTask.FromResult(FileOpenResult.Opened(stream, stream.CanSeek ? content.Length : 0, null));
    }

    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("A form body never writes a file.");

    /// <summary>A read-only memory stream that remembers being disposed and can refuse to seek.</summary>
    internal sealed class TrackedStream(byte[] content, bool refusesSeek) : MemoryStream(content, writable: false)
    {
        internal bool IsDisposed { get; private set; }

        public override bool CanSeek => !refusesSeek && base.CanSeek;

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
