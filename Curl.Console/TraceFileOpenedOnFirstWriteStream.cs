using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The write-only stream behind a <c>--trace</c> or <c>--trace-ascii</c> file, which opens the file,
/// truncating it, on the first write rather than up front, as curl 8.21.0 does: a run that fails
/// before its first trace event leaves no file, and an existing one untouched (STD-226).
/// </summary>
/// <param name="fileSystem">Opens the file; its open for writing must complete synchronously.</param>
/// <param name="path">The <c>--trace</c> or <c>--trace-ascii</c> file name.</param>
/// <param name="standardError">Where the dump goes when the file cannot be opened; never disposed by this stream.</param>
/// <remarks>
/// A failed write, flush or close of the file is ignored, as curl ignores it (CGR-026, BL-3830).
/// Trace events are reported synchronously, so the open cannot be awaited:
/// <see cref="IFileSystem.OpenForWriteAsync" /> must hand back a completed task, as the physical
/// file system does (ADR-1357).
/// </remarks>
internal sealed class TraceFileOpenedOnFirstWriteStream(IFileSystem fileSystem, string path, Stream standardError) : Stream
{
    private readonly Lock openGate = new();

    private Stream? target;

    private Stream? openedFile;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Opens the file, truncating it, before any write; does nothing once it is open.</summary>
    /// <exception cref="InvalidOperationException">The file system's open for writing did not complete synchronously.</exception>
    public void OpenNow() => OpenOnce();

    /// <summary>Flushes what the dump goes to; does nothing while no file is open, and opens none.</summary>
    public override void Flush() => target?.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The file system's open for writing did not complete synchronously.</exception>
    public override void Write(ReadOnlySpan<byte> buffer) => OpenOnce().Write(buffer);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        openedFile?.Dispose();
        base.Dispose(disposing);
    }

    private Stream OpenOnce()
    {
        lock (openGate)
        {
            return target ??= Open();
        }
    }

    private Stream Open()
    {
        ValueTask<FileOpenResult> opening = fileSystem.OpenForWriteAsync(
            path, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None);
        if (!opening.IsCompleted)
        {
            throw new InvalidOperationException("The trace file must open synchronously.");
        }

        if (opening.Result.Content is not { } file)
        {
            return standardError;
        }

        openedFile = new WriteFailureIgnoringStream(file);
        return openedFile;
    }
}
