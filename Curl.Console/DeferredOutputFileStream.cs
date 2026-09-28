using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The write-only stream behind one output file - an <c>-o</c> name, or a remote name from
/// <c>-O</c> or <c>-J</c>, under any <c>--output-dir</c> - which opens the file on the first
/// write rather than up front, as curl 8.21.0 does.
/// </summary>
/// <param name="fileSystem">Opens the file.</param>
/// <param name="path">The file to open, as the runner resolved it: rewritten by <see cref="WindowsOutputFileNameSanitizer" /> on Windows and put under <c>--output-dir</c>.</param>
/// <param name="writeMode">
/// <see cref="FileWriteMode.Truncate" /> for a whole transfer;
/// <see cref="FileWriteMode.Append" /> when <c>-C</c> / <c>--continue-at</c> resumes it, as
/// curl opens the file <c>"ab"</c> then.
/// </param>
/// <param name="clobber">
/// The <c>--clobber</c> / <c>--no-clobber</c> choice: <see langword="false" /> opens a whole
/// transfer's file only when nothing is there and otherwise takes the first free
/// <c>&lt;path&gt;.1</c> ... <c>&lt;path&gt;.99</c>; <see langword="true" /> overwrites even a
/// <c>-J</c> name; <see langword="null" />, the default, overwrites an <c>-o</c> or <c>-O</c> file
/// and not a <c>-J</c> one.
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
/// A result with <see cref="TransferResult.TimeConditionUnmet" /> set is the exception: it
/// creates no file and leaves an existing one untouched, as curl 8.21.0 does for an unmet
/// <c>-z</c>/<c>--time-cond</c>.
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
internal sealed class DeferredOutputFileStream(IFileSystem fileSystem, string path, FileWriteMode writeMode, bool? clobber = null) : Stream
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

    /// <summary>
    /// The highest number <c>--no-clobber</c> puts after a taken name: curl 8.21.0 tries
    /// <c>.1</c> to <c>.99</c> (measured 2026-09-28).
    /// </summary>
    internal const int LastNumberedName = 99;

    private Stream? file;
    private FileWriteMode openMode = writeMode;
    private long? failedWriteLength;
    private long lengthWhenOpened;

    /// <summary>
    /// Gets the file this stream writes: the path it was created with, until
    /// <see cref="TryOpenUnderNameAsync" /> renames it for <c>-J</c>.
    /// </summary>
    internal string Path { get; private set; } = path;

    /// <summary>Gets a value indicating whether the file has been opened.</summary>
    internal bool IsOpen => file is not null;

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
            throw new IOException($"Could not create the output file {Path}.");
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
    /// when a successful transfer wrote nothing, was not skipped by an unmet time
    /// condition, and the empty file could not be created; otherwise <paramref name="result" />.
    /// </returns>
    internal async ValueTask<TransferResult> CompleteAsync(TransferResult result)
    {
        if (failedWriteLength is { } length)
        {
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                "client returned ERROR on write of " + length.ToString(CultureInfo.InvariantCulture) + " bytes");
        }

        bool createsEmptyFile = file is null && result.IsSuccess && !result.TimeConditionUnmet;

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
            .OpenForWriteAsync(Path, openMode, CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        Adopt(opened.Content);

        return opened.IsOpen;
    }

    /// <summary>
    /// Renames the file to the <c>-J</c> name a <c>Content-Disposition</c> header gave and opens
    /// it now, as curl 8.21.0 does when it reads that header; a failure makes the transfer's
    /// result curl's <c>client returned ERROR on write of N bytes</c>, N being the header line's
    /// length, and sets <see cref="OpenFailureWarning" />.
    /// </summary>
    /// <param name="newPath">The file to open.</param>
    /// <param name="headerLineLength">The length of the <c>Content-Disposition</c> line, CR LF included.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns><see langword="true" /> when the file is open.</returns>
    /// <remarks>
    /// <para>
    /// The file is opened <see cref="FileWriteMode.CreateNew" />, as curl opens it with
    /// <c>O_EXCL</c>: a file already under that name - even one another process created a
    /// moment ago - is kept, and the open fails with curl's
    /// <c>Warning: Failed to open the file x.txt: File exists</c>. <c>--no-clobber</c> takes the
    /// first free numbered name instead, and <c>--clobber</c> overwrites it, as curl 8.21.0 does
    /// (measured 2026-09-28, BL-492 Notes).
    /// </para>
    /// <para>
    /// An empty name fails as curl's <c>fopen("")</c> does, with <c>No such file or directory</c>,
    /// without asking the file system.
    /// </para>
    /// </remarks>
    internal async ValueTask<bool> TryOpenUnderNameAsync(string newPath, int headerLineLength, CancellationToken cancellationToken)
    {
        Path = newPath;
        openMode = clobber == true ? FileWriteMode.Truncate : FileWriteMode.CreateNew;
        if (newPath.Length == 0)
        {
            FailOpen(OutputFileOpenWarning.For(newPath, FileAccessStatus.NotFound), headerLineLength);

            return false;
        }

        if (await TryOpenAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            failedWriteLength = headerLineLength;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Records that the file could not be opened for a reason found before the open, such as a
    /// <c>-J</c> name arriving for a file a <c>-C</c> resume already opened.
    /// </summary>
    /// <param name="warning">The warning line curl prints for it, or <see langword="null" /> for none.</param>
    /// <param name="writeLength">The length curl's <c>client returned ERROR on write of N bytes</c> reports.</param>
    internal void FailOpen(string? warning, int writeLength)
    {
        OpenFailureWarning = warning;
        failedWriteLength = writeLength;
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
    /// Cuts the file back to the length it had when it was opened, before <c>--retry</c> runs
    /// the transfer again, as curl 8.21.0 does (<c>ftruncate</c> to the length at open): a
    /// retried 503 written with <c>-o</c> left one copy of the body, not two (measured
    /// 2026-09-27, BL-241 Notes). A file not yet opened, or one that cannot seek, is left as
    /// it is.
    /// </summary>
    internal void TruncateForRetry()
    {
        if (file is { CanSeek: true } opened)
        {
            opened.Flush();
            opened.SetLength(lengthWhenOpened);
            opened.Position = lengthWhenOpened;
        }
    }

    /// <summary>
    /// Takes <paramref name="opened" /> as the file, and remembers its length for
    /// <see cref="TruncateForRetry" />.
    /// </summary>
    /// <param name="opened">The file just opened, or <see langword="null" /> when the open failed.</param>
    private void Adopt(Stream? opened)
    {
        file = opened;
        lengthWhenOpened = opened is { CanSeek: true } ? opened.Length : 0;
    }

    /// <summary>
    /// Opens the file in its write mode - <see cref="FileWriteMode.CreateNew" /> once it has a
    /// <c>-J</c> name, or under <c>--no-clobber</c> - with <see cref="CreateMode" />. Under
    /// <c>--no-clobber</c> a name already taken moves on to <see cref="OpenFirstFreeNumberedNameAsync" />.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open file, or <see langword="null" /> when it could not be opened.</returns>
    /// <remarks>
    /// The warning names the file asked for, not a numbered one, as curl 8.21.0's does (measured
    /// 2026-09-28, BL-492 Notes).
    /// </remarks>
    private async ValueTask<Stream?> TryOpenAsync(CancellationToken cancellationToken)
    {
        string requestedPath = Path;
        FileWriteMode mode = clobber == false && openMode == FileWriteMode.Truncate ? FileWriteMode.CreateNew : openMode;
        FileOpenResult opened = await OpenAsync(mode, cancellationToken).ConfigureAwait(false);
        if (clobber == false && opened.Status == FileAccessStatus.AlreadyExists)
        {
            opened = await OpenFirstFreeNumberedNameAsync(cancellationToken).ConfigureAwait(false);
        }

        Adopt(opened.Content);

        if (!opened.IsOpen)
        {
            OpenFailureWarning = OutputFileOpenWarning.For(requestedPath, opened.Status);
        }

        return file;
    }

    /// <summary>
    /// Opens <see cref="Path" /> with <paramref name="mode" /> and <see cref="CreateMode" />.
    /// </summary>
    /// <param name="mode">How existing content is treated.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The file system's answer.</returns>
    private ValueTask<FileOpenResult> OpenAsync(FileWriteMode mode, CancellationToken cancellationToken) =>
        fileSystem.OpenForWriteAsync(Path, mode, CreateMode, cancellationToken);

    /// <summary>
    /// Creates <c>&lt;path&gt;.1</c>, then <c>.2</c>, and so on while each is already taken, up to
    /// <c>.99</c>, as curl 8.21.0's <c>--no-clobber</c> does: it stops before <c>.100</c>, whatever
    /// its manual says (measured 2026-09-28, BL-492 Notes). <see cref="Path" /> is left on the last
    /// name tried, which is what <c>%{filename_effective}</c> prints even when none was free.
    /// </summary>
    /// <param name="cancellationToken">Cancels the opens.</param>
    /// <returns>The last open's answer.</returns>
    private async ValueTask<FileOpenResult> OpenFirstFreeNumberedNameAsync(CancellationToken cancellationToken)
    {
        string takenPath = Path;
        FileOpenResult opened;
        int number = 0;
        do
        {
            number++;
            Path = takenPath + "." + number.ToString(CultureInfo.InvariantCulture);
            opened = await OpenAsync(FileWriteMode.CreateNew, cancellationToken).ConfigureAwait(false);
        }
        while (opened.Status == FileAccessStatus.AlreadyExists && number < LastNumberedName);

        return opened;
    }
}
