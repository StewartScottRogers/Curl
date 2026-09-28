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
/// The order of work matches curl 8.21.0's <c>lib/file.c</c>: open, apply
/// <c>-z</c>/<c>--time-cond</c> (an unmet condition ends the transfer as a success with
/// nothing written, headers included), write the pseudo-headers, return early for
/// <c>-I</c>/<c>--head</c>, resolve <c>-r</c>/<c>--range</c> or
/// <c>-C</c>/<c>--continue-at</c> into a window of the file — so a resume failure comes
/// after the headers — then move the body in 16-kilobyte chunks, writing no more than
/// <see cref="ITransferContext.MaxFileSize" /> allows and failing with exit 63 when there is
/// more. An upload under
/// <c>--crlf</c> (<see cref="ITransferContext.ConvertLineEndings" />) converts each chunk
/// on the way to the destination; a download never does. The destination is opened with
/// <see cref="ITransferContext.CreateFileMode" />, curl's <c>--create-file-mode</c>, as the
/// mode a newly created file receives on a POSIX system.
/// </para>
/// <para>
/// The exit codes are curl's, not the nearest-looking ones: every failure to open a
/// source is exit 37 (<see cref="CurlExitCode.FileCouldntReadFile" />) whatever the
/// operating system said, never exit 78 or exit 9; a destination that will not open is
/// exit 23 (<see cref="CurlExitCode.WriteError" />); a download source that fails to be
/// read after the open ends the body there and exits 0, because curl treats the failed
/// read as the end of the file; an upload source of known length that fails to be read
/// is exit 26 (<see cref="CurlExitCode.ReadError" />), and one of unknown length - standard
/// input - ends there with exit 0; an upload destination that fails to be written after
/// the open is exit 55 (<see cref="CurlExitCode.SendError" />); a download offset past the end of the
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
/// Of the options on <see cref="ITransferContext" />, the download path ignores
/// <see cref="ITransferContext.ConvertLineEndings" /> and
/// <see cref="ITransferContext.CreateFileMode" />, and the upload path ignores
/// <see cref="ITransferContext.Range" />, <see cref="ITransferContext.NoBody" />,
/// <see cref="ITransferContext.TimeCondition" />, <see cref="ITransferContext.HeaderOutput" />
/// and <see cref="ITransferContext.MaxFileSize" />. Both ignore
/// <see cref="ITransferContext.TimeProvider" />, deliberately: nothing in a local file
/// transfer is timed or retried, and <c>-z</c> compares against the timestamp the open
/// reported rather than against now. Both also ignore the options that belong to other
/// protocols. The table "Transfer options, per direction" in
/// <c>Curl.Protocol.File.UnitLibrary\CLAUDE.md</c> gives every member, per direction,
/// with the method that reads it.
/// </para>
/// </remarks>
public sealed class FileProtocolHandler(IFileSystem fileSystem) : IProtocolHandler
{
    /// <summary>
    /// The chunk size for a <c>file://</c> download body, which is curl's
    /// <c>CURL_MAX_WRITE_SIZE</c>. It is observable — it is the size of every write to
    /// the output but the last — so it is pinned here rather than left to
    /// <see cref="Stream.CopyToAsync(Stream)" />, whose buffer is five times larger.
    /// </summary>
    private const int ChunkSize = 16384;

    /// <summary>
    /// The chunk size for a <c>file://</c> upload body. curl 8.21.0 reads a <c>-T</c>
    /// source 65536 bytes at a time, counted from the start of the file even when
    /// <c>-C</c> skips part of it, which is observable in the exit 26 message: a
    /// 100000-byte source that fails to read from byte 99000 on reports
    /// <c>only 65536/100000</c>.
    /// </summary>
    private const int UploadChunkSize = 65536;

    /// <summary>
    /// The one scheme this handler serves. Checked against curl 8.21.0's
    /// <c>--version</c> protocol list, which names <c>file</c> and nothing else that
    /// belongs to this library.
    /// </summary>
    private static readonly string[] Schemes = ["file"];

    /// <summary>
    /// The Unix epoch as <see cref="WholeSeconds" /> counts it: the value libcurl holds as a
    /// <c>time_t</c> of 0.
    /// </summary>
    private static readonly long UnixEpochWholeSeconds = WholeSeconds(DateTimeOffset.UnixEpoch);

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
    /// on <see cref="ByteRange" />: the command line refuses a negative <c>-C</c> with exit 2
    /// before any URL is looked at, so this branch only answers a context built by hand, and a
    /// bad option should end a transfer rather than the process. It is kept as an unreachable
    /// defensive default, not curl behaviour; ADR-0007 records why. Checking it once, up here,
    /// is also what keeps a download and
    /// an upload answering it identically, since below this point the two paths diverge
    /// and meet again only in the shared chunked copy.
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

        // curl 8.21.0 draws its meter for a download that got past the open and failed
        // later (exit 63, exit 36) and never for one whose open failed (exit 37), measured
        // in BL-129. No byte counts follow: curl's file:// status line stays at zero.
        context.Progress.ReportTransferStarted();

        Stream source = opened.Content;

        TransferResult result;

        await using (source.ConfigureAwait(false))
        {
            result = await DownloadFromAsync(context, source, opened).ConfigureAwait(false);
        }

        // -R/--remote-time is applied by whoever owns the output file, so a successful
        // download, an unmet -z included, hands the source's timestamp back in whole
        // seconds, the resolution curl 8.21.0 applies it at. A failure carries none.
        return result.IsSuccess
            ? result with { SourceLastWriteTimeUtc = TruncateToWholeSeconds(opened.LastWriteTimeUtc) }
            : result;
    }

    /// <summary>
    /// Drops everything below the second from a timestamp, keeping its offset.
    /// </summary>
    /// <param name="timestamp">The timestamp, or <see langword="null" /> when unknown.</param>
    /// <returns>
    /// The timestamp with zero sub-second ticks, or <see langword="null" /> when
    /// <paramref name="timestamp" /> is.
    /// </returns>
    private static DateTimeOffset? TruncateToWholeSeconds(DateTimeOffset? timestamp) =>
        timestamp is { } value
            ? new DateTimeOffset(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Offset)
            : null;

    /// <summary>
    /// Applies the time condition, emits the headers, applies every other option that can
    /// stop the body, then moves it.
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
        if (!MeetsTimeCondition(context.TimeCondition, opened.LastWriteTimeUtc))
        {
            return TransferResult.TimeConditionNotMet();
        }

        if (await WriteHeadersAsync(context, opened).ConfigureAwait(false) is { } headerFailure)
        {
            return headerFailure;
        }

        TransferResult result = context.NoBody
            ? TransferResult.Success(0)
            : await DownloadBodyAsync(context, source, opened.Length).ConfigureAwait(false);

        return WithPseudoHeaders(result, opened);
    }

    /// <summary>
    /// Reports the pseudo-headers the transfer produced, so <c>%{num_headers}</c> counts
    /// them. curl 8.21.0 counts them once they were produced, whether or not <c>-D</c>
    /// wrote them anywhere and whatever the body did after, as measured in BL-285.
    /// </summary>
    /// <param name="result">The outcome of the body stage.</param>
    /// <param name="opened">The metadata the pseudo-headers were built from.</param>
    /// <returns>
    /// <paramref name="result" /> with a report of the pseudo-headers and, because a report
    /// replaces <see cref="TransferResult.BytesTransferred" /> as the source of
    /// <c>%{size_download}</c>, its byte count.
    /// </returns>
    private static TransferResult WithPseudoHeaders(TransferResult result, FileOpenResult opened) =>
        result with
        {
            Report = new TransferReport
            {
                PseudoHeaders = FileTransferMessages.PseudoHeaders(opened.Length, opened.LastWriteTimeUtc),
                DownloadSize = result.BytesTransferred,
            },
        };

    /// <summary>
    /// Applies <c>-C</c>/<c>--continue-at</c> or <c>-r</c>/<c>--range</c> to the opened
    /// source, then moves the window of it they select.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="source">The opened source, which this method does not dispose.</param>
    /// <param name="length">The length of the opened file.</param>
    /// <returns>The outcome of the download.</returns>
    private static ValueTask<TransferResult> DownloadBodyAsync(
        ITransferContext context,
        Stream source,
        long length)
    {
        if (!TryResolveWindow(
            context,
            length,
            out long start,
            out long count,
            out string resumeErrorMessage))
        {
            return ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.BadDownloadResume, resumeErrorMessage));
        }

        if (!TrySeekToStart(source, start))
        {
            return ValueTask.FromResult(
                TransferResult.Failure(
                    CurlExitCode.BadDownloadResume,
                    FileTransferMessages.ResumeFailed));
        }

        return CopyWindowAsync(context, source, count);
    }

    /// <summary>
    /// Moves <paramref name="count" /> bytes from the source's current position to the
    /// output, stopping at <c>--max-filesize</c>.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="source">The opened source, already at the first byte to send.</param>
    /// <param name="count">How many bytes to send.</param>
    /// <returns>The outcome of the download.</returns>
    private static ValueTask<TransferResult> CopyWindowAsync(
        ITransferContext context,
        Stream source,
        long count)
    {
        return CopyAsync(
            source,
            context.Output,
            ChunkSize,
            ChunkSize,
            count,
            context.MaxFileSize is > 0 and long maxFileSize ? maxFileSize : long.MaxValue,
            static chunk => chunk,
            static (_, transferred) => TransferResult.Success(transferred),
            static (offered, accepted, transferred) => TransferResult.Failure(
                CurlExitCode.WriteError,
                FileTransferMessages.OutputWriteFailed(offered, accepted),
                transferred),
            context.CancellationToken);
    }

    /// <summary>
    /// Moves the source to the first byte to send, when that is not its beginning.
    /// </summary>
    /// <param name="source">The opened source.</param>
    /// <param name="start">The first byte position to send.</param>
    /// <returns>
    /// <see langword="false" /> when the source must be moved and cannot seek.
    /// </returns>
    private static bool TrySeekToStart(Stream source, long start)
    {
        if (start <= 0)
        {
            return true;
        }

        // A character device or a FIFO - file:///dev/stdin - opens as a stream that
        // cannot seek, and Seek would throw NotSupportedException out of a handler that
        // promises to return every transfer failure. curl 8.21.0's lib/file.c answers a
        // failed lseek with exit 36, so this does too.
        if (!source.CanSeek)
        {
            return false;
        }

        source.Seek(start, SeekOrigin.Begin);
        return true;
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
        // An upload is started before its destination opens: curl 8.21.0 draws the meter
        // even when the destination will not open (exit 23), measured in BL-129, because
        // lib/file.c opens it in the transfer phase rather than at connect.
        context.Progress.ReportTransferStarted();

        FileWriteMode mode = context.ResumeFrom is > 0
            ? FileWriteMode.Append
            : FileWriteMode.Truncate;

        var opened = await fileSystem
            .OpenForWriteAsync(path.OsPath, mode, context.CreateFileMode, context.CancellationToken)
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
    /// Skips whatever <c>-C</c>/<c>--continue-at</c> asked for, then moves the body,
    /// converting line endings on the way when <c>--crlf</c> asked for it.
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
        // curl knows the length of a -T file and not of standard input, and only a known
        // length turns a failed read into exit 26; the curl tool reports a failed read as the
        // end of the file, so with no length to fall short of, the upload simply ends there.
        long? needed = upload.CanSeek ? upload.Length - upload.Position : null;
        long skip = context.ResumeFrom ?? 0;

        bool skipped = await TrySkipAsync(upload, skip, context.CancellationToken)
            .ConfigureAwait(false);

        if (!skipped)
        {
            return TransferResult.Success(0);
        }

        // curl reads the source in UploadChunkSize chunks from its start, skipped bytes
        // included, so the first read after a -C skip only reaches the next chunk boundary,
        // and a failed read reports the boundary it started from: measured with a 200000-byte
        // source unreadable from byte 150000, -C 10, -C 70000 and -C 140000 all print
        // only 131072/200000, with size_upload 131062, 61072 and 0.
        int firstChunkSize = UploadChunkSize - (int)(skip % UploadChunkSize);

        // A fresh converter per upload, so the carriage return it remembers never leaks
        // from one transfer into the next. Bytes skipped by -C are not seen by it.
        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> convertChunk = context.ConvertLineEndings
            ? new CrlfUploadConverter(UploadChunkSize).Convert
            : static chunk => chunk;

        return await CopyAsync(
                upload,
                destination,
                UploadChunkSize,
                firstChunkSize,
                long.MaxValue,
                long.MaxValue,
                convertChunk,
                (consumed, transferred) => needed is { } length
                    ? new TransferResult(
                        CurlExitCode.ReadError,
                        transferred,
                        FileTransferMessages.UploadSourceReadFailed(
                            (skip + consumed) / UploadChunkSize * UploadChunkSize,
                            length))
                    : TransferResult.Success(transferred),
                static (_, _, transferred) => new TransferResult(
                    CurlExitCode.SendError,
                    transferred,
                    FileTransferMessages.DestinationWriteFailed),
                context.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Moves up to <paramref name="count" /> bytes in <paramref name="chunkSize" /> chunks,
    /// telling a failed read apart from a failed write.
    /// </summary>
    /// <param name="source">Where the bytes come from.</param>
    /// <param name="destination">Where they go.</param>
    /// <param name="chunkSize">
    /// The most to read at once: <see cref="ChunkSize" /> for a download,
    /// <see cref="UploadChunkSize" /> for an upload.
    /// </param>
    /// <param name="firstChunkSize">
    /// The most to read the first time, which is <paramref name="chunkSize" /> unless an
    /// upload's <c>-C</c> skip left the source part-way into a chunk.
    /// </param>
    /// <param name="count">
    /// How many bytes at most to read, or <see cref="long.MaxValue" /> to run to the end of
    /// <paramref name="source" />.
    /// </param>
    /// <param name="maxWritten">
    /// How many bytes at most to write, <c>--max-filesize</c>, or <see cref="long.MaxValue" />
    /// for no limit. A chunk that would pass it is written only up to it, and the copy then
    /// fails with exit 63, as curl 8.21.0 does.
    /// </param>
    /// <param name="convertChunk">
    /// Turns each chunk read into the chunk written: the chunk itself, or its
    /// <c>--crlf</c> conversion on an upload.
    /// </param>
    /// <param name="reportReadFailure">
    /// Builds the outcome of a failed read, from the bytes read and the bytes written
    /// before it. Every direction answers this differently, and one of the answers is
    /// success.
    /// </param>
    /// <param name="reportWriteFailure">
    /// Builds the outcome of a failed write, from the size of the chunk offered, how many
    /// of its bytes the destination accepted before failing, and the bytes written before
    /// that chunk.
    /// </param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>
    /// A success carrying the number of bytes written, which after a <c>--crlf</c>
    /// conversion is more than were read, as curl 8.21.0's <c>size_upload</c> is; whatever
    /// <paramref name="reportReadFailure" /> or <paramref name="reportWriteFailure" /> makes
    /// of a failure; or exit 63 carrying the bytes written when there was more to write
    /// than <paramref name="maxWritten" />.
    /// </returns>
    private static async ValueTask<TransferResult> CopyAsync(
        Stream source,
        Stream destination,
        int chunkSize,
        int firstChunkSize,
        long count,
        long maxWritten,
        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> convertChunk,
        Func<long, long, TransferResult> reportReadFailure,
        Func<long, long, long, TransferResult> reportWriteFailure,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[chunkSize];
        long consumed = 0;
        long transferred = 0;
        int nextChunkSize = firstChunkSize;

        while (consumed < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int wanted = (int)Math.Min(nextChunkSize, count - consumed);
            nextChunkSize = chunkSize;
            int read = await TryReadAsync(source, buffer, wanted, cancellationToken)
                .ConfigureAwait(false);

            if (read < 0)
            {
                return reportReadFailure(consumed, transferred);
            }

            if (read == 0)
            {
                break;
            }

            ReadOnlyMemory<byte> chunk = convertChunk(buffer.AsMemory(0, read));
            int allowed = (int)Math.Min(chunk.Length, maxWritten - transferred);

            // At the limit exactly, the next chunk writes nothing at all, not an empty write.
            if (allowed > 0
                && await TryWriteAsync(destination, chunk[..allowed], cancellationToken).ConfigureAwait(false)
                    is { } accepted)
            {
                return reportWriteFailure(allowed, accepted, transferred);
            }

            consumed += read;
            transferred += allowed;

            if (allowed < chunk.Length)
            {
                return new TransferResult(
                    CurlExitCode.FilesizeExceeded,
                    transferred,
                    FileTransferMessages.MaxFileSizeExceeded(maxWritten, transferred));
            }
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
    /// Writes one chunk, reporting a failure as a count rather than an exception.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="buffer">The bytes to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <see langword="null" /> when the write succeeded; otherwise how many of its bytes the
    /// destination accepted before failing, which is
    /// <see cref="OutputWriteFailedException.BytesAccepted" /> when the destination reported
    /// it and 0 for any other failure.
    /// </returns>
    private static async ValueTask<int?> TryWriteAsync(
        Stream destination,
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (OperationCanceledException)
        {
            // As in TryReadAsync: a cancelled write leaves through the token, so the type
            // is OperationCanceledException and not TaskCanceledException.
            cancellationToken.ThrowIfCancellationRequested();

            return 0;
        }
        catch (OutputWriteFailedException failure)
        {
            return failure.BytesAccepted;
        }
        catch (IOException)
        {
            return 0;
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
    /// <returns>
    /// <see langword="false" /> when a read failed, which ends the upload as a success with
    /// nothing sent: the curl tool reports a failed read of standard input as its end, and
    /// an offset past the end of the source is the measured exit 0 with nothing written.
    /// </returns>
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

        byte[] buffer = new byte[UploadChunkSize];

        for (long skipped = 0; skipped < count;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int wanted = (int)Math.Min(UploadChunkSize, count - skipped);
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
    /// Writes curl's synthesised header block, when the caller asked for headers at all,
    /// one line per write as curl does. A failed write reports its message to
    /// <see cref="ITransferContext.Events" /> as an information line too, as libcurl's
    /// <c>failf</c> does: curl 8.21.0 under <c>-v</c> prints
    /// <c>* client returned ERROR on write of 20 bytes</c> before <c>curl: (23)</c>
    /// (measured 2026-09-26, BL-111 Notes).
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="opened">The metadata that came with the open.</param>
    /// <returns>
    /// <see langword="null" /> when the headers were written or not asked for, otherwise
    /// the exit 23 failure, reporting the length of the line that failed as the bytes
    /// offered.
    /// </returns>
    private static async ValueTask<TransferResult?> WriteHeadersAsync(
        ITransferContext context,
        FileOpenResult opened)
    {
        if (context.HeaderOutput is not { } headerOutput)
        {
            return null;
        }

        foreach (string line in FileTransferMessages.PseudoHeaderLines(opened.Length, opened.LastWriteTimeUtc))
        {
            byte[] bytes = Encoding.ASCII.GetBytes(line);

            if (await TryWriteAsync(headerOutput, bytes, context.CancellationToken).ConfigureAwait(false) is not null)
            {
                string message = FileTransferMessages.HeaderWriteFailed(bytes.Length);
                context.Events.ReportInfo(message);

                return TransferResult.Failure(CurlExitCode.WriteError, message);
            }
        }

        return null;
    }

    /// <summary>
    /// Applies <c>-z</c>/<c>--time-cond</c> to the timestamp the open reported.
    /// </summary>
    /// <param name="condition">The condition, or <see langword="null" /> for none.</param>
    /// <param name="lastWriteTimeUtc">
    /// The file's last-write timestamp, or <see langword="null" /> when the file system
    /// could not determine one.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the body should be transferred. An unmet condition is
    /// a success with no body, not a failure.
    /// </returns>
    /// <remarks>
    /// Both operands are truncated to whole seconds, because curl sees a local file's
    /// timestamp in whole seconds, and both comparisons are strict: at equality neither
    /// direction transfers, as measured on curl 8.21.0. Truncation is this handler's
    /// comparison rule; <see cref="TimeCondition" /> itself is left as the command line
    /// parsed it. An unknown timestamp transfers whichever way the condition runs: a
    /// condition that cannot be evaluated must not silently suppress the data, which is
    /// what libcurl 8.21.0's <c>Curl_meets_timecondition</c> does for an unknown document
    /// time. That function also reads a <c>time_t</c> of 0 on either side as unknown, so a
    /// timestamp or a condition date that truncates to the whole second of the Unix epoch
    /// transfers too, whichever way the condition runs. The epoch is unknown only here:
    /// the <c>Last-Modified</c> header line still reports it.
    /// </remarks>
    private static bool MeetsTimeCondition(
        TimeCondition? condition,
        DateTimeOffset? lastWriteTimeUtc)
    {
        return condition is null
            || lastWriteTimeUtc is not { } knownLastWriteTimeUtc
            || MeetsKnownTimeCondition(condition, knownLastWriteTimeUtc);
    }

    /// <summary>
    /// Compares a known file timestamp with a time condition, in whole seconds.
    /// </summary>
    /// <param name="condition">The condition to apply.</param>
    /// <param name="lastWriteTimeUtc">The file's last-modified timestamp.</param>
    /// <returns>
    /// <see langword="true" /> when the body should be transferred, including when either
    /// side truncates to the Unix epoch, which curl reads as unknown.
    /// </returns>
    private static bool MeetsKnownTimeCondition(
        TimeCondition condition,
        DateTimeOffset lastWriteTimeUtc)
    {
        long fileSeconds = WholeSeconds(lastWriteTimeUtc);
        long conditionSeconds = WholeSeconds(condition.Value);

        if (fileSeconds == UnixEpochWholeSeconds || conditionSeconds == UnixEpochWholeSeconds)
        {
            return true;
        }

        return condition.Kind == TimeConditionKind.IfModifiedSince
            ? fileSeconds > conditionSeconds
            : fileSeconds < conditionSeconds;
    }

    /// <summary>
    /// Counts the whole seconds from <see cref="DateTimeOffset.MinValue" /> to
    /// <paramref name="value" /> in UTC, dropping any fraction of a second.
    /// </summary>
    /// <param name="value">The timestamp to truncate.</param>
    /// <returns>The timestamp as a count of whole seconds.</returns>
    private static long WholeSeconds(DateTimeOffset value) =>
        value.UtcTicks / TimeSpan.TicksPerSecond;

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
    /// is exit 36. A start exactly equal to the length is a success sending nothing. The
    /// command line refuses <c>-C</c> with <c>-r</c> (exit 2, as curl 8.21.0 does), so a
    /// context carrying both never comes from it; built by hand, <c>-C</c> wins. A negative
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
