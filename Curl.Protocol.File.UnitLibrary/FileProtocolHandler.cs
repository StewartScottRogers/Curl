using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File;

/// <summary>
/// Serves the <c>file</c> scheme by reading and writing local files through an injected
/// <see cref="IFileSystem" />.
/// </summary>
/// <param name="fileSystem">
/// The file system to open local paths through. No <see cref="FileStream" /> is ever
/// constructed here, so the handler's tests run entirely in memory.
/// </param>
/// <remarks>
/// <para>
/// The order of work matches curl 8.21.0's <c>lib/file.c</c>: open, write the
/// pseudo-headers, return early for <c>-I</c>/<c>--head</c>, apply
/// <c>-z</c>/<c>--time-cond</c>, resolve <c>-r</c>/<c>--range</c> or
/// <c>-C</c>/<c>--continue-at</c> into a window of the file, then move the body in
/// 16-kilobyte chunks.
/// </para>
/// <para>
/// The exit codes are curl's, not the nearest-looking ones: every failure to open a
/// source is exit 37 (<see cref="CurlExitCode.FileCouldntReadFile" />) whatever the
/// operating system said, never exit 78 or exit 9; a destination that will not open is
/// exit 23 (<see cref="CurlExitCode.WriteError" />); a read that fails after the open is
/// exit 26 (<see cref="CurlExitCode.ReadError" />); a download offset past the end of the
/// file is exit 36 (<see cref="CurlExitCode.BadDownloadResume" />), where an offset exactly
/// equal to the length is a success with no bytes. A transfer failure is returned as a
/// <see cref="TransferResult" /> and never thrown; only cancellation leaves this handler
/// as an exception.
/// </para>
/// <para>
/// Three exit-36 cases are spelled out because each was measured rather than reasoned
/// about. A <c>-C</c> offset past the end of the source fails; a suffix range fails only
/// when it exceeds the length by more than one — <c>-r -11</c> on a ten-byte file is the
/// whole file and exit 0, <c>-r -12</c> is exit 36 — and it fails with a different message
/// (<c>Could not resume download</c>) from the <c>-C</c> case; and an upload resume offset
/// past the end of the upload source is not an error at all, curl 8.21.0 exiting 0 having
/// written nothing. The two directions genuinely disagree upstream, so this handler
/// disagrees with itself in the same way.
/// </para>
/// <para>
/// Of the options on <see cref="ITransferContext" />,
/// <see cref="ITransferContext.TimeProvider" /> is deliberately unused: nothing in a
/// local file transfer is timed or retried, and <c>-z</c> compares against the timestamp
/// the open reported rather than against now.
/// </para>
/// </remarks>
public sealed class FileProtocolHandler(IFileSystem fileSystem) : IProtocolHandler
{
    /// <summary>
    /// The chunk size for a <c>file://</c> body, which is curl's
    /// <c>CURL_MAX_WRITE_SIZE</c>. It is observable — it is the size of every write to
    /// the output but the last — so it is pinned here rather than left to
    /// <see cref="Stream.CopyToAsync(Stream)" />, whose buffer is five times larger.
    /// </summary>
    private const int ChunkSize = 16384;

    /// <summary>
    /// The one scheme this handler serves. Checked against curl 8.21.0's
    /// <c>--version</c> protocol list, which names <c>file</c> and nothing else that
    /// belongs to this library.
    /// </summary>
    private static readonly string[] Schemes = ["file"];

    private readonly IFileSystem fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    /// <remarks>
    /// A negative <see cref="ITransferContext.ResumeFrom" /> is refused here, before any
    /// file system call, as exit 36 (<see cref="CurlExitCode.BadDownloadResume" />) with
    /// the same message as a resume offset past the end of the file. It is a returned
    /// failure rather than an <see cref="ArgumentOutOfRangeException" />, unlike the guards
    /// on <see cref="ByteRange" />: the command line layer that will normally reject a
    /// malformed <c>-C</c> value is not written yet, and a bad option should end a transfer
    /// rather than the process. Checking it once, up here, is also what keeps a download and
    /// an upload answering it identically, since below this point the two paths share
    /// nothing.
    /// </remarks>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.CancellationToken.ThrowIfCancellationRequested();

        if (!FileUrlPath.TryParse(context.Url, out var path))
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, FileTransferMessages.BadUrl);
        }

        if (context.ResumeFrom is < 0)
        {
            return TransferResult.Failure(
                CurlExitCode.BadDownloadResume,
                FileTransferMessages.ResumeFailed);
        }

        return context.Upload is { } upload
            ? await UploadAsync(context, path, upload).ConfigureAwait(false)
            : await DownloadAsync(context, path).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the source and, whatever happens next, disposes it.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="path">The parsed URL path.</param>
    /// <returns>The outcome of the download.</returns>
    private async ValueTask<TransferResult> DownloadAsync(ITransferContext context, FileUrlPath path)
    {
        var opened = await fileSystem
            .OpenForReadAsync(path.OsPath, context.CancellationToken)
            .ConfigureAwait(false);

        if (!opened.IsOpen || opened.Content is null)
        {
            return TransferResult.Failure(
                CurlExitCode.FileCouldntReadFile,
                FileTransferMessages.CouldNotOpenForReading(path.UrlPath));
        }

        Stream source = opened.Content;

        await using (source.ConfigureAwait(false))
        {
            return await DownloadFromAsync(context, source, opened).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Emits the headers, applies every option that can stop the body, then moves it.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="source">The opened source, which this method does not dispose.</param>
    /// <param name="opened">The metadata that came with the open.</param>
    /// <returns>The outcome of the download.</returns>
    private static async ValueTask<TransferResult> DownloadFromAsync(
        ITransferContext context,
        Stream source,
        FileOpenResult opened)
    {
        if (!await TryWriteHeadersAsync(context, opened).ConfigureAwait(false))
        {
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                FileTransferMessages.OutputWriteFailed);
        }

        if (context.NoBody || !MeetsTimeCondition(context.TimeCondition, opened.LastWriteTimeUtc))
        {
            return TransferResult.Success(0);
        }

        if (!TryResolveWindow(
            context,
            opened.Length,
            out long start,
            out long count,
            out string resumeErrorMessage))
        {
            return TransferResult.Failure(CurlExitCode.BadDownloadResume, resumeErrorMessage);
        }

        if (start > 0)
        {
            source.Seek(start, SeekOrigin.Begin);
        }

        return await CopyAsync(
                source,
                context.Output,
                count,
                FileTransferMessages.OutputWriteFailed,
                context.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the destination and, whatever happens next, disposes it.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="path">The parsed URL path.</param>
    /// <param name="upload">
    /// The stream to upload from, owned by the caller and so left undisposed here.
    /// </param>
    /// <returns>The outcome of the upload.</returns>
    private async ValueTask<TransferResult> UploadAsync(
        ITransferContext context,
        FileUrlPath path,
        Stream upload)
    {
        // curl 8.21.0's lib/file.c tests the value of the resume offset, not whether one
        // was supplied: -C 0 truncates exactly as no -C at all does, so only a positive
        // offset appends.
        FileWriteMode mode = context.ResumeFrom is > 0
            ? FileWriteMode.Append
            : FileWriteMode.Truncate;

        var opened = await fileSystem
            .OpenForWriteAsync(path.OsPath, mode, context.CancellationToken)
            .ConfigureAwait(false);

        if (!opened.IsOpen || opened.Content is null)
        {
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                FileTransferMessages.CannotOpenForWriting(path.OsPath));
        }

        Stream destination = opened.Content;

        await using (destination.ConfigureAwait(false))
        {
            return await UploadIntoAsync(context, upload, destination).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Skips whatever <c>-C</c>/<c>--continue-at</c> asked for, then moves the body.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="upload">The stream to upload from.</param>
    /// <param name="destination">The opened destination, which this method does not dispose.</param>
    /// <returns>The outcome of the upload.</returns>
    private static async ValueTask<TransferResult> UploadIntoAsync(
        ITransferContext context,
        Stream upload,
        Stream destination)
    {
        bool skipped = await TrySkipAsync(
                upload,
                context.ResumeFrom ?? 0,
                context.CancellationToken)
            .ConfigureAwait(false);

        if (!skipped)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, FileTransferMessages.ReadFailed);
        }

        return await CopyAsync(
                upload,
                destination,
                long.MaxValue,
                FileTransferMessages.DestinationWriteFailed,
                context.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Moves up to <paramref name="count" /> bytes in <see cref="ChunkSize" /> chunks,
    /// telling a failed read apart from a failed write.
    /// </summary>
    /// <param name="source">Where the bytes come from.</param>
    /// <param name="destination">Where they go.</param>
    /// <param name="count">
    /// How many bytes at most, or <see cref="long.MaxValue" /> to run to the end of
    /// <paramref name="source" />.
    /// </param>
    /// <param name="writeErrorMessage">The message to report if a write fails.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>
    /// A success carrying the number of bytes moved, exit 26 for a failed read or exit 23
    /// for a failed write.
    /// </returns>
    private static async ValueTask<TransferResult> CopyAsync(
        Stream source,
        Stream destination,
        long count,
        string writeErrorMessage,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[ChunkSize];
        long transferred = 0;

        while (transferred < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int wanted = (int)Math.Min(ChunkSize, count - transferred);
            int read = await TryReadAsync(source, buffer, wanted, cancellationToken)
                .ConfigureAwait(false);

            if (read < 0)
            {
                return TransferResult.Failure(
                    CurlExitCode.ReadError,
                    FileTransferMessages.ReadFailed);
            }

            if (read == 0)
            {
                break;
            }

            bool written = await TryWriteAsync(
                    destination,
                    buffer.AsMemory(0, read),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!written)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, writeErrorMessage);
            }

            transferred += read;
        }

        return TransferResult.Success(transferred);
    }

    /// <summary>
    /// Reads one chunk, reporting a failure as a count rather than an exception so the
    /// caller stays a straight line.
    /// </summary>
    /// <param name="source">The stream to read.</param>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="wanted">How many bytes of <paramref name="buffer" /> to fill.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The number of bytes read, zero at the end of the stream, or minus one when the read
    /// failed.
    /// </returns>
    private static async ValueTask<int> TryReadAsync(
        Stream source,
        byte[] buffer,
        int wanted,
        CancellationToken cancellationToken)
    {
        try
        {
            return await source
                .ReadAsync(buffer.AsMemory(0, wanted), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A cancelled stream reports TaskCanceledException; rethrowing through the
            // token narrows that to the OperationCanceledException a caller expects. An
            // uncancelled token means the stream cancelled itself, which is a read failure.
            cancellationToken.ThrowIfCancellationRequested();

            return -1;
        }
        catch (IOException)
        {
            return -1;
        }
    }

    /// <summary>
    /// Writes one chunk, reporting a failure as <see langword="false" /> rather than an
    /// exception.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="buffer">The bytes to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="false" /> when the write failed.</returns>
    private static async ValueTask<bool> TryWriteAsync(
        Stream destination,
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            // As in TryReadAsync: a cancelled write leaves through the token, so the type
            // is OperationCanceledException and not TaskCanceledException.
            cancellationToken.ThrowIfCancellationRequested();

            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Discards the first <paramref name="count" /> bytes of an upload source, by seeking
    /// when it can be seeked and by reading when it cannot — <c>-T -</c> uploads from
    /// standard input.
    /// </summary>
    /// <param name="source">The stream to advance.</param>
    /// <param name="count">How many bytes to skip; zero or less does nothing.</param>
    /// <param name="cancellationToken">Cancels the skip.</param>
    /// <returns><see langword="false" /> when a read failed.</returns>
    /// <remarks>
    /// The skip is relative to wherever the caller left the stream, so an upload source
    /// handed over already positioned keeps that position. An offset past the end of the
    /// source is deliberately not an error: curl 8.21.0 exits 0 and writes nothing in that
    /// case and never reports exit 36 for it the way the download direction does. Do not
    /// make the two directions symmetric; the asymmetry is upstream's.
    /// </remarks>
    private static async ValueTask<bool> TrySkipAsync(
        Stream source,
        long count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return true;
        }

        if (source.CanSeek)
        {
            // Relative, not absolute: SeekOrigin.Begin would silently discard a position
            // the caller had already set. Seeking past the end is allowed and leaves the
            // copy that follows with nothing to read, which is the measured exit 0.
            source.Seek(count, SeekOrigin.Current);

            return true;
        }

        byte[] buffer = new byte[ChunkSize];

        for (long skipped = 0; skipped < count;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int wanted = (int)Math.Min(ChunkSize, count - skipped);
            int read = await TryReadAsync(source, buffer, wanted, cancellationToken)
                .ConfigureAwait(false);

            if (read < 0)
            {
                return false;
            }

            if (read == 0)
            {
                break;
            }

            skipped += read;
        }

        return true;
    }

    /// <summary>
    /// Writes curl's synthesised header block, when the caller asked for headers at all.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="opened">The metadata that came with the open.</param>
    /// <returns><see langword="false" /> when the header write failed.</returns>
    private static async ValueTask<bool> TryWriteHeadersAsync(
        ITransferContext context,
        FileOpenResult opened)
    {
        if (context.HeaderOutput is not { } headerOutput)
        {
            return true;
        }

        byte[] headers = Encoding.ASCII.GetBytes(
            FileTransferMessages.PseudoHeaders(opened.Length, opened.LastWriteTimeUtc));

        return await TryWriteAsync(headerOutput, headers, context.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies <c>-z</c>/<c>--time-cond</c> to the timestamp the open reported.
    /// </summary>
    /// <param name="condition">The condition, or <see langword="null" /> for none.</param>
    /// <param name="lastWriteTimeUtc">The file's last-write timestamp.</param>
    /// <returns>
    /// <see langword="true" /> when the body should be transferred. An unmet condition is
    /// a success with no body, not a failure.
    /// </returns>
    private static bool MeetsTimeCondition(
        TimeCondition? condition,
        DateTimeOffset lastWriteTimeUtc) =>
        condition is null
        || (condition.Kind == TimeConditionKind.IfModifiedSince
            ? lastWriteTimeUtc > condition.Value
            : lastWriteTimeUtc <= condition.Value);

    /// <summary>
    /// Turns <c>-C</c>/<c>--continue-at</c> or <c>-r</c>/<c>--range</c> into the window of
    /// the file to send.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="length">The length of the opened file.</param>
    /// <param name="start">On success, the first byte position to send.</param>
    /// <param name="count">On success, how many bytes to send from there.</param>
    /// <param name="errorMessage">
    /// On failure, the exit 36 message to report, which is not the same string for every
    /// exit 36.
    /// </param>
    /// <returns>
    /// <see langword="false" /> when the start is strictly past the end of the file, which
    /// is exit 36. A start exactly equal to the length is a success sending nothing, and
    /// <c>-C</c> wins over <c>-r</c> when a caller somehow supplies both. A negative
    /// <see cref="ITransferContext.ResumeFrom" /> never arrives here: <see cref="ExecuteAsync" />
    /// has already refused it.
    /// </returns>
    private static bool TryResolveWindow(
        ITransferContext context,
        long length,
        out long start,
        out long count,
        out string errorMessage)
    {
        start = 0;
        count = length;
        errorMessage = FileTransferMessages.ResumeFailed;

        if (context.ResumeFrom is { } resumeFrom)
        {
            if (resumeFrom > length)
            {
                return false;
            }

            start = resumeFrom;
            count = length - start;

            return true;
        }

        return context.Range is not { } range
            || TryResolveRange(range, length, out start, out count, out errorMessage);
    }

    /// <summary>
    /// Turns one <see cref="ByteRange" /> into a window, in each of its three forms.
    /// </summary>
    /// <param name="range">The requested range.</param>
    /// <param name="length">The length of the opened file.</param>
    /// <param name="start">On success, the first byte position to send.</param>
    /// <param name="count">On success, how many bytes to send from there.</param>
    /// <param name="errorMessage">On failure, the exit 36 message to report.</param>
    /// <returns>
    /// <see langword="false" /> when the first byte position is strictly past the end of
    /// the file, or when a suffix asks for more than one byte more than the file holds. A
    /// suffix of exactly one byte more than the length is still the whole file, measured on
    /// curl 8.21.0: <c>-r -11</c> of ten bytes succeeds and <c>-r -12</c> does not, and
    /// <c>-r -7</c> of five bytes does not either, so the boundary is the length plus one
    /// and not the length. That failure carries
    /// <see cref="FileTransferMessages.CouldNotResumeDownload" />, a different string from
    /// the <c>-C</c> and range-start failures.
    /// </returns>
    private static bool TryResolveRange(
        ByteRange range,
        long length,
        out long start,
        out long count,
        out string errorMessage)
    {
        errorMessage = FileTransferMessages.ResumeFailed;
        start = 0;
        count = 0;

        if (range.Kind == ByteRangeKind.Suffix)
        {
            long suffixLength = range.SuffixLength ?? 0;

            if (suffixLength > length + 1)
            {
                errorMessage = FileTransferMessages.CouldNotResumeDownload;

                return false;
            }

            start = Math.Max(0, length - suffixLength);
            count = length - start;

            return true;
        }

        start = range.FirstBytePosition ?? 0;

        if (start > length)
        {
            return false;
        }

        // Clamp the end position to the last byte that exists before subtracting, rather
        // than taking the shorter of two spans afterwards. The arithmetic version
        // overflowed: ByteRange.Bounded(0, long.MaxValue) is legal - the factory only
        // requires the end not to precede the start - and (long.MaxValue - 0) + 1 wraps
        // to long.MinValue, so the copy loop's "transferred < count" was false on the
        // first test and the handler reported success having written nothing. A range
        // asking for the whole file returned an empty one, with exit 0.
        count = range.LastBytePosition is { } lastBytePosition
            ? (Math.Min(lastBytePosition, length - 1) - start) + 1
            : length - start;

        // An empty file has no last byte to clamp to, so the line above computes 1 for
        // a zero-length source. Nothing is transferred either way, but the reported
        // count has to be zero.
        if (count < 0 || length == 0)
        {
            count = 0;
        }

        return true;
    }
}
