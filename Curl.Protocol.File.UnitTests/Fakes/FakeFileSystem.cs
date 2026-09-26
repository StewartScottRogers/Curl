using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File.Fakes;

/// <summary>
/// An in-memory <see cref="IFileSystem" /> that records every call made to it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Calls" /> is the primary observable of these tests: for a protocol with no
/// wire, the ordered list of opens — with the path and, for a write, the mode — is the
/// equivalent of the bytes an FTP handler sends. No member of this class touches a disk.
/// </para>
/// <para>
/// Neither open inspects its cancellation token, because opening a real file does not
/// either: <see cref="FileStream" />'s constructor is synchronous and ignores
/// cancellation entirely. Honouring it here would also make every pre-cancelled test
/// vacuous — the call would never be recorded and the exception would come from this fake
/// rather than from the handler's own guard, so deleting that guard would break nothing.
/// With the token ignored, an empty <see cref="Calls" /> is proof the handler refused
/// before it opened anything.
/// </para>
/// </remarks>
public sealed class FakeFileSystem : IFileSystem
{
    /// <summary>
    /// The timestamp every entry reports unless a test chooses another. Fixed so the
    /// <c>Last-Modified</c> pseudo-header is a constant rather than a moving target.
    /// </summary>
    public static readonly DateTimeOffset DefaultLastWriteTimeUtc =
        new(2026, 6, 24, 12, 34, 56, TimeSpan.Zero);

    private readonly List<FileSystemCall> calls = [];
    private readonly Dictionary<string, FakeFileEntry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileAccessStatus> readFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileAccessStatus> writeFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Stream> writeDestinations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Stream> openedReadStreams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TrackedMemoryStream> captures = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets every call made to this file system, in order.
    /// </summary>
    public IReadOnlyList<FileSystemCall> Calls => calls;

    /// <summary>
    /// Adds a file with the default timestamp.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="content">The file's bytes.</param>
    public void AddFile(string path, byte[] content) =>
        AddFile(path, content, DefaultLastWriteTimeUtc);

    /// <summary>
    /// Adds a file.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="lastWriteTimeUtc">The file's timestamp.</param>
    public void AddFile(string path, byte[] content, DateTimeOffset lastWriteTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(path);

        entries[path] = FakeFileEntry.ForFile(content, lastWriteTimeUtc);
    }

    /// <summary>
    /// Adds a directory, which a read open rejects.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    public void AddDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        entries[path] = FakeFileEntry.ForDirectory();
    }

    /// <summary>
    /// Adds a file whose read open hands back a stream the test owns.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="content">The stream to hand back.</param>
    /// <param name="length">The length the open reports.</param>
    public void AddFileReadingFrom(string path, Stream content, long length)
    {
        ArgumentNullException.ThrowIfNull(path);

        entries[path] = FakeFileEntry.ForStream(content, length, DefaultLastWriteTimeUtc);
    }

    /// <summary>
    /// Forces an open for reading at <paramref name="path" /> to fail.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="status">The failure to report.</param>
    public void FailOpenForRead(string path, FileAccessStatus status)
    {
        ArgumentNullException.ThrowIfNull(path);

        readFailures[path] = status;
    }

    /// <summary>
    /// Forces an open for writing at <paramref name="path" /> to fail.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="status">The failure to report.</param>
    public void FailOpenForWrite(string path, FileAccessStatus status)
    {
        ArgumentNullException.ThrowIfNull(path);

        writeFailures[path] = status;
    }

    /// <summary>
    /// Makes an open for writing at <paramref name="path" /> hand back a stream the test
    /// owns instead of the fake's own capture buffer.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <param name="destination">The stream to hand back.</param>
    public void WriteInto(string path, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(destination);

        writeDestinations[path] = destination;
    }

    /// <summary>
    /// Gets the bytes written to the capture buffer for <paramref name="path" />.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <returns>The captured bytes.</returns>
    /// <exception cref="InvalidOperationException">
    /// Nothing was ever opened for writing at that path.
    /// </exception>
    public byte[] WrittenBytes(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return captures.TryGetValue(path, out var capture)
            ? capture.ToArray()
            : throw new InvalidOperationException($"Nothing was opened for writing at '{path}'.");
    }

    /// <summary>
    /// Gets the stream handed back by the read open of <paramref name="path" />, so a test
    /// can ask whether the handler disposed it.
    /// </summary>
    /// <param name="path">The operating-system path.</param>
    /// <returns>The stream the handler was given.</returns>
    /// <exception cref="InvalidOperationException">
    /// Nothing was ever opened for reading at that path, or the stream handed back does
    /// not record its disposal.
    /// </exception>
    public IRecordingStream ReadStreamFor(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!openedReadStreams.TryGetValue(path, out var stream))
        {
            throw new InvalidOperationException($"Nothing was opened for reading at '{path}'.");
        }

        if (stream is not IRecordingStream recording)
        {
            throw new InvalidOperationException(
                $"The stream handed back for '{path}' does not record disposal.");
        }

        return recording;
    }

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        calls.Add(FileSystemCall.Read(path));

        if (readFailures.TryGetValue(path, out var forced))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(forced));
        }

        if (!entries.TryGetValue(path, out var entry))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        if (entry.IsDirectory)
        {
            return ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.IsDirectory));
        }

        Stream content = entry.PresetContent ?? new TrackedMemoryStream(entry.Content);
        openedReadStreams[path] = content;

        return ValueTask.FromResult(
            FileOpenResult.Opened(content, entry.Length, entry.LastWriteTimeUtc));
    }

    /// <inheritdoc />
    public ValueTask<FileOpenResult> OpenForWriteAsync(
        string path,
        FileWriteMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        calls.Add(FileSystemCall.Write(path, mode));

        if (writeFailures.TryGetValue(path, out var forced))
        {
            return ValueTask.FromResult(FileOpenResult.Failed(forced));
        }

        entries.TryGetValue(path, out var existing);

        byte[] kept = mode == FileWriteMode.Append && existing is not null ? existing.Content : [];
        DateTimeOffset lastWriteTimeUtc = existing?.LastWriteTimeUtc ?? DefaultLastWriteTimeUtc;

        if (writeDestinations.TryGetValue(path, out var destination))
        {
            return ValueTask.FromResult(
                FileOpenResult.Opened(destination, kept.Length, lastWriteTimeUtc));
        }

        var capture = new TrackedMemoryStream(kept)
        {
            Position = kept.Length,
        };

        captures[path] = capture;

        return ValueTask.FromResult(
            FileOpenResult.Opened(capture, kept.Length, lastWriteTimeUtc));
    }
}
