using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The write-only stream behind one <c>-o</c> / <c>--output</c> file, which opens the file
/// on the first write rather than up front, as curl 8.21.0 does.
/// </summary>
/// <param name="fileSystem">Opens the file.</param>
/// <param name="path">The <c>-o</c> value, as typed.</param>
/// <param name="writeMode">
/// <see cref="FileWriteMode.Truncate" /> for a whole transfer;
/// <see cref="FileWriteMode.Append" /> when <c>-C</c> / <c>--continue-at</c> resumes it, as
/// curl opens the file <c>"ab"</c> then.
/// </param>
/// <remarks>
/// <para>
/// Opening late is what gives curl's messages: when the file cannot be created, the
/// first write fails with an <see cref="IOException" />, and
/// <see cref="CompleteAsync" /> replaces the handler's write-failure result with curl's
/// <c>client returned ERROR on write of N bytes</c>, N being the size of that first write.
/// </para>
/// <para>
/// A successful transfer that wrote nothing still creates an empty file, in
/// <see cref="CompleteAsync" />; when that open fails the result is exit 23 with no
/// message, which is what curl 8.21.0 prints under <c>-sS</c> (measured 2026-09-26).
/// </para>
/// <para>
/// Either failed open also sets <see cref="OpenFailureWarning" />, the warning curl prints
/// before its exit 23 line unless <c>-s</c> was given.
/// </para>
/// <para>
/// Only asynchronous writes are supported: the file system opens files asynchronously,
/// and every handler writes with
/// <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)" />.
/// </para>
/// </remarks>
internal sealed class DeferredOutputFileStream(IFileSystem fileSystem, string path, FileWriteMode writeMode) : Stream
{
    /// <summary>
    /// The mode a newly created <c>-o</c> file receives on a POSIX system, before the umask:
    /// <c>0666</c>, what curl's <c>fopen</c> of the file uses. <c>--create-file-mode</c> does
    /// not apply to <c>-o</c>.
    /// </summary>
    internal const UnixFileMode CreateMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

    private Stream? file;
    private long? failedWriteLength;

    /// <summary>
    /// Gets curl's <c>Warning: Failed to open the file &lt;path&gt;: &lt;reason&gt;</c> line once an
    /// open of the file has failed, or <see langword="null" /> while none has.
    /// </summary>
    internal string? OpenFailureWarning { get; private set; }

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

    /// <inheritdoc />
    public override void Flush() => file?.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        file?.FlushAsync(cancellationToken) ?? Task.CompletedTask;

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; write asynchronously instead.</exception>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The -o file opens asynchronously; write with WriteAsync.");

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <exception cref="IOException">The file could not be created.</exception>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Stream? target = file ?? await TryOpenAsync(cancellationToken).ConfigureAwait(false);

        if (target is null)
        {
            failedWriteLength ??= buffer.Length;
            throw new IOException($"Could not create the output file {path}.");
        }

        await target.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Settles the transfer's result once the handler has finished with the file.
    /// </summary>
    /// <param name="result">The handler's result.</param>
    /// <returns>
    /// curl's <c>client returned ERROR on write of N bytes</c> when the file could not be
    /// created, N being the size of the first write that failed; exit 23 with no message
    /// when a successful transfer wrote nothing
    /// and the empty file could not be created; otherwise <paramref name="result" />.
    /// </returns>
    internal async ValueTask<TransferResult> CompleteAsync(TransferResult result)
    {
        if (failedWriteLength is { } length)
        {
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                "client returned ERROR on write of " + length.ToString(CultureInfo.InvariantCulture) + " bytes");
        }

        bool createsEmptyFile = file is null && result.IsSuccess;

        return createsEmptyFile && await TryOpenAsync(CancellationToken.None).ConfigureAwait(false) is null
            ? new TransferResult(CurlExitCode.WriteError, 0)
            : result;
    }

    /// <summary>
    /// Opens the file now rather than on the first write, as curl 8.21.0 does for a resumed
    /// transfer. A failed open here does not set <see cref="OpenFailureWarning" />: curl
    /// reports it with its own <c>curl: cannot open</c> line instead.
    /// </summary>
    /// <returns><see langword="true" /> when the file is open.</returns>
    internal async ValueTask<bool> TryOpenNowAsync()
    {
        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(path, writeMode, CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        file = opened.Content;

        return opened.IsOpen;
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (file is not null)
        {
            await file.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        file?.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Opens the file in its write mode, with <see cref="CreateMode" />.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open file, or <see langword="null" /> when it could not be opened.</returns>
    private async ValueTask<Stream?> TryOpenAsync(CancellationToken cancellationToken)
    {
        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(path, writeMode, CreateMode, cancellationToken)
            .ConfigureAwait(false);
        file = opened.Content;

        if (!opened.IsOpen)
        {
            OpenFailureWarning = OutputFileOpenWarning.For(path, opened.Status);
        }

        return file;
    }
}
