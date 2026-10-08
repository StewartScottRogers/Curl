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
/// <param name="connectionNumbers">
/// The count each transfer past its open takes its connection number from: the run's shared
/// count, as curl 8.21.0 numbers a <c>file://</c> transfer with every connection the run
/// opens (BL-977).
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
/// <see cref="ITransferContext.Range" />, <see cref="ITransferContext.RangeText" />, <see cref="ITransferContext.NoBody" />,
/// <see cref="ITransferContext.TimeCondition" />, <see cref="ITransferContext.HeaderOutput" />
/// and <see cref="ITransferContext.MaxFileSize" />. Both read
/// <see cref="ITransferContext.TimeProvider" /> only to time the diagnostic log's
/// transfer-end line: nothing in a local file transfer is timed out or retried, and
/// <c>-z</c> compares against the timestamp the open reported rather than against now.
/// Each step is written to <see cref="ITransferContext.DiagnosticLog" /> under the
/// <c>file</c> component (<see cref="FileTransferLog" />). Both also ignore the options that belong to other
/// protocols. The table "Transfer options, per direction" in
/// <c>Curl.Protocol.File.UnitLibrary\CLAUDE.md</c> gives every member, per direction,
/// with the method that reads it.
/// </para>
/// </remarks>
public sealed class FileProtocolHandler(IFileSystem fileSystem, IConnectionNumbers connectionNumbers) : IProtocolHandler
{
    /// <summary>
    /// The write size for a <c>file://</c> download body, which is curl's
    /// <c>CURL_MAX_WRITE_SIZE</c>. It is observable — it is the size of every write to
    /// the output but the last, and the <c>passed 16384</c> of an exit 23 message — so
    /// each <see cref="DownloadReadSize" /> read is written in slices of this size.
    /// </summary>
    private const int ChunkSize = 16384;

    /// <summary>
    /// The read size for a <c>file://</c> download body. curl 8.21.0 reads the source
    /// 102399 bytes at a time and reports each read as one <c>&lt;= Recv data</c> block:
    /// a 1000000-byte file traces nine blocks of 102399 bytes and one of 78409 (BL-936),
    /// and a 300000-byte file unreadable from byte 150000 delivers 102399 bytes and exits 0
    /// (BL-976).
    /// </summary>
    private const int DownloadReadSize = 102399;

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
    /// The Unix seconds of 9999-12-31T23:59:59Z, the last whole second
    /// <see cref="DateTimeOffset" />, and so <see cref="FileOpenResult.LastWriteTimeUtc" />,
    /// holds.
    /// </summary>
    private const long MaxDateTimeOffsetUnixSeconds = 253_402_300_799;

    private readonly IFileSystem fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>
    /// The count each transfer past its open takes its connection number from: curl 8.21.0
    /// numbers <c>file://</c> transfers as connections, <c>#0</c>, <c>#1</c> and on, a
    /// transfer whose open failed taking no number (measured in BL-936), in the one count
    /// shared with every connection the run opens, so <c>curl -v http://h/ file:///a</c>
    /// shuts down <c>#1</c> for the file (measured in BL-977).
    /// </summary>
    private readonly IConnectionNumbers connectionNumbers =
        connectionNumbers ?? throw new ArgumentNullException(nameof(connectionNumbers));

    /// <summary>
    /// Initializes a new instance of the <see cref="FileProtocolHandler" /> class that numbers
    /// its transfers in a count of its own, from <c>0</c>, for a run with no networked
    /// connections to share one with.
    /// </summary>
    /// <param name="fileSystem">The file system to open local paths through.</param>
    public FileProtocolHandler(IFileSystem fileSystem)
        : this(fileSystem, new ConnectionNumberSequence())
    {
    }

    /// <summary>
    /// Gets a value indicating whether a <c>file://</c> URL naming a directory lists the
    /// directory's entry names, as curl 8.21.0's Linux and macOS builds do with
    /// <c>opendir</c>/<c>readdir</c>, rather than failing with exit 37, as its Windows
    /// build does because it cannot open a directory. It is the platform's answer unless a
    /// test sets the other one.
    /// </summary>
    internal bool ListsDirectories { get; init; } = !OperatingSystem.IsWindows();

    /// <summary>
    /// Gets the reader of a source's raw last-write time, for a time past 9999 that
    /// <see cref="FileOpenResult.LastWriteTimeUtc" /> cannot carry: <c>GetFileTime</c> on
    /// Windows, and none yet elsewhere. It is the platform's reader unless a test sets another.
    /// </summary>
    internal ISourceLastWriteReader SourceLastWriteReader { get; init; } = PlatformSourceLastWriteReader();

    /// <summary>
    /// Gets the last source time, in Unix seconds, the platform's curl can represent, or
    /// <see langword="null" /> when it represents every 64-bit time. curl 8.21.0's Windows
    /// build reads a source time through the C runtime's <c>_fstat64</c>, which fails from
    /// local 3002-01-01T00:00:00 on: curl then holds <c>time_t</c> -1, so <c>-R</c> leaves
    /// the output's time alone, <c>-z</c> compares against -1 and the header block says
    /// <c>Last-Modified: Thu, 31 Dec 1969 23:59:59 GMT</c> (measured 2026-10-03, BL-1423,
    /// ADR-0411). The OpenSSL builds read <c>st_mtime</c> whole. It is the platform's answer
    /// unless a test sets another.
    /// </summary>
    internal long? LastRepresentableUnixSeconds { get; init; } = PlatformLastRepresentableUnixSeconds();

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

        long started = context.TimeProvider.GetTimestamp();
        TransferResult result = await TransferAsync(context).ConfigureAwait(false);
        new FileTransferLog(context.DiagnosticLog).Ended(result, context.TimeProvider.GetElapsedTime(started));
        return result;
    }

    /// <summary>
    /// Parses the URL's path, refuses one that decodes to a NUL and a negative resume offset,
    /// then uploads or downloads.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <returns>The outcome of the transfer.</returns>
    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        if (!FileUrlPath.TryParse(context.Url, out var path))
        {
            return ReportFailure(
                context.Events,
                TransferResult.Failure(CurlExitCode.UrlMalformat, FileTransferMessages.BadUrl));
        }

        // curl 8.21.0's file_connect decodes the path with REJECT_ZERO and writes no failf,
        // so a path decoding to a NUL ends with exit 3 and no -v line (BL-1451, ADR-0416).
        if (path.OsPath.Contains('\0', StringComparison.Ordinal))
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, FileTransferMessages.UrlMalformed);
        }

        if (context.ResumeFrom is < 0)
        {
            return ReportFailure(
                context.Events,
                TransferResult.Failure(CurlExitCode.BadDownloadResume, FileTransferMessages.ResumeFailed));
        }

        return context.Upload is { } upload
            ? await UploadAsync(context, path, upload).ConfigureAwait(false)
            : await DownloadAsync(context, path).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports a failed transfer's message as an information line, as libcurl's
    /// <c>failf</c> does under <c>-v</c> and <c>--trace</c>: curl 8.21.0 prints
    /// <c>* Could not open file ...</c> before <c>curl: (37)</c>, measured in BL-936. Exit 55
    /// and the exit 33 of <c>-r</c> text that names no range are the exceptions: each message
    /// is only <c>curl_easy_strerror</c>'s text, which no <c>failf</c> wrote, so curl prints no
    /// line for it (<c>curl -sv -r 5-2 file://...</c> writes only <c>* shutting down
    /// connection #0</c>, measured in BL-1322).
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="result">The outcome of the transfer.</param>
    /// <returns><paramref name="result" />, unchanged.</returns>
    private static TransferResult ReportFailure(ITransferEvents events, TransferResult result)
    {
        if (result.ErrorMessage is { } message
            && message is not (FileTransferMessages.DestinationWriteFailed or FileTransferMessages.RangeNotDelivered or FileTransferMessages.DirectoryListingFailed))
        {
            events.ReportInfo(message);
        }

        return result;
    }

    /// <summary>
    /// Reports how a transfer that got past its open ended: its failure, if any, then that
    /// its data is done, so the progress meter ends its line, then the line libcurl writes
    /// as it lets the connection go. curl 8.21.0 under <c>-v</c> writes the meter's line end
    /// before <c>* shutting down connection #0</c>, measured in BL-936.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="result">The outcome of the transfer.</param>
    /// <param name="connectionNumber">The number the transfer's connection was given.</param>
    /// <returns><paramref name="result" />, unchanged.</returns>
    /// <remarks>
    /// libcurl's <c>multi_done</c> counts exit 23 and exit 26 as premature and closes the
    /// connection; every other outcome, success included, shuts it down. Measured in BL-936.
    /// </remarks>
    private static TransferResult ReportConnectionEnd(
        ITransferContext context,
        TransferResult result,
        long connectionNumber)
    {
        ITransferEvents events = context.Events;
        ReportFailure(events, result);
        context.Progress.ReportTransferDone();
        events.ReportInfo(result.ExitCode is CurlExitCode.WriteError or CurlExitCode.ReadError
            ? FileTransferMessages.ClosingConnection(connectionNumber)
            : FileTransferMessages.ShuttingDownConnection(connectionNumber));

        return result;
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

        var transferLog = new FileTransferLog(context.DiagnosticLog);
        if (DirectoryListerFor(opened) is { } lister)
        {
            return await ListDirectoryAsync(context, path, opened, lister).ConfigureAwait(false);
        }

        if (!opened.IsOpen || opened.Content is null)
        {
            transferLog.OpenFailed(path.OsPath, "reading", opened);
            return ReportFailure(
                context.Events,
                TransferResult.Failure(
                    CurlExitCode.FileCouldntReadFile,
                    FileTransferMessages.CouldNotOpenForReading(path.UrlPath)));
        }

        transferLog.OpenedForReading(path.OsPath, opened.Length);
        long connectionNumber = connectionNumbers.NumberNextConnection();

        // curl 8.21.0 draws its meter for a download that got past the open and failed
        // later (exit 63, exit 36) and never for one whose open failed (exit 37), measured
        // in BL-129. No byte counts follow: curl's file:// status line stays at zero.
        context.Progress.ReportTransferStarted();

        Stream source = opened.Content;
        SourceLastWrite lastWrite = LastWriteOf(opened);

        TransferResult result;

        await using (source.ConfigureAwait(false))
        {
            result = await DownloadFromAsync(context, source, opened, lastWrite).ConfigureAwait(false);
        }

        return EndDownload(context, result, connectionNumber, lastWrite);
    }

    /// <summary>
    /// Gets the source's last-write time as curl holds it: the time
    /// <see cref="FileOpenResult.LastWriteTimeUtc" /> carries, unless the raw time the
    /// <see cref="SourceLastWriteReader" /> reads lies past what that or the platform's curl
    /// can hold.
    /// </summary>
    /// <param name="opened">The metadata that came with the open.</param>
    /// <returns>The source's last-write time.</returns>
    private SourceLastWrite LastWriteOf(FileOpenResult opened)
    {
        long? rawUnixSeconds = opened.Content is { } content
            ? SourceLastWriteReader.ReadLastWriteUnixSeconds(content)
            : null;
        long lastRepresentable = Math.Min(LastRepresentableUnixSeconds ?? long.MaxValue, MaxDateTimeOffsetUnixSeconds);

        if (rawUnixSeconds is not { } raw || raw <= lastRepresentable)
        {
            return new SourceLastWrite(opened.LastWriteTimeUtc?.ToUnixTimeSeconds(), opened.LastWriteTimeUtc, true);
        }

        return raw > LastRepresentableUnixSeconds
            ? new SourceLastWrite(-1, DateTimeOffset.FromUnixTimeSeconds(-1), false)
            : new SourceLastWrite(raw, null, true);
    }

    /// <summary>
    /// The platform's source time reader: <c>GetFileTime</c> on Windows, <c>statx</c> on Linux,
    /// <c>fgetattrlist</c> on macOS, none elsewhere.
    /// </summary>
    /// <returns>The reader.</returns>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "ADR-0083: the platform picks the branch.")]
    private static ISourceLastWriteReader PlatformSourceLastWriteReader() =>
        OperatingSystem.IsWindows() ? new Win32SourceLastWriteReader()
        : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? new PosixSourceLastWriteReader()
        : new NoRawSourceLastWriteReader();

    /// <summary>The platform's curl's last representable source time; none off Windows.</summary>
    /// <returns>The Unix seconds, or <see langword="null" />.</returns>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "ADR-0083: the platform picks the branch.")]
    private static long? PlatformLastRepresentableUnixSeconds() =>
        OperatingSystem.IsWindows() ? WindowsCrtLastRepresentableUnixSeconds() : null;

    /// <summary>
    /// The last local second, in Unix seconds, the Windows C runtime's <c>_fstat64</c>
    /// converts: 3001-12-31T23:59:59 local time, measured on curl 8.21.0 in BL-1423.
    /// </summary>
    /// <returns>The Unix seconds of that local time.</returns>
    private static long WindowsCrtLastRepresentableUnixSeconds()
    {
        var firstUnrepresentableLocal = new DateTime(3002, 1, 1);
        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(firstUnrepresentableLocal);

        return new DateTimeOffset(firstUnrepresentableLocal, offset).ToUnixTimeSeconds() - 1;
    }

    /// <summary>
    /// A source's last-write time as curl holds it.
    /// </summary>
    /// <param name="UnixSeconds">
    /// The time in whole Unix seconds, which <c>-z</c> compares, or <see langword="null" />
    /// when unknown.
    /// </param>
    /// <param name="HeaderTime">
    /// The time the header block's <c>Last-Modified</c> line reports, or
    /// <see langword="null" /> for no line.
    /// </param>
    /// <param name="AppliesToRemoteTime">
    /// Whether <c>-R</c> stamps the output with <see cref="UnixSeconds" />: not for a time
    /// the platform's curl could not represent.
    /// </param>
    private readonly record struct SourceLastWrite(long? UnixSeconds, DateTimeOffset? HeaderTime, bool AppliesToRemoteTime)
    {
        /// <summary>Gets the time <c>-R</c> stamps the output with, or <see langword="null" /> for none.</summary>
        public long? RemoteTimeUnixSeconds => AppliesToRemoteTime ? UnixSeconds : null;
    }

    /// <summary>
    /// Reports how a download that got past its open ended and hands back its outcome.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="result">The outcome of the download.</param>
    /// <param name="connectionNumber">The number the transfer's connection was given.</param>
    /// <param name="lastWrite">The source's last-write time.</param>
    /// <returns><paramref name="result" />, with the source's timestamp when it succeeded.</returns>
    private static TransferResult EndDownload(
        ITransferContext context,
        TransferResult result,
        long connectionNumber,
        SourceLastWrite lastWrite)
    {
        ReportConnectionEnd(context, result, connectionNumber);

        // -R/--remote-time is applied by whoever owns the output file, so a successful
        // download, an unmet -z included, hands the source's timestamp back in whole
        // Unix seconds, the resolution curl 8.21.0 applies it at. A failure carries none.
        return result.IsSuccess
            ? result with { SourceLastWriteUnixSeconds = lastWrite.RemoteTimeUnixSeconds }
            : result;
    }

    /// <summary>
    /// Picks the lister that serves an open which found a directory: the file system,
    /// when this handler lists directories and the file system can list.
    /// </summary>
    /// <param name="opened">The outcome of the open.</param>
    /// <returns>
    /// The lister, or <see langword="null" /> when the open did not find a directory, this
    /// handler answers a directory as the Windows build does, or the file system cannot
    /// list - each of which keeps exit 37.
    /// </returns>
    private IDirectoryLister? DirectoryListerFor(FileOpenResult opened) =>
        opened.Status == FileAccessStatus.IsDirectory && ListsDirectories
            ? fileSystem as IDirectoryLister
            : null;

    /// <summary>
    /// Lists a directory as curl 8.21.0's Linux and macOS builds do (<c>lib/file.c</c>,
    /// <c>file_do</c>): numbers the connection and starts the meter as for a file, then
    /// applies <c>-z</c>, writes the <c>-i</c> header block, stops there for <c>-I</c>, and
    /// writes the entry names.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="path">The parsed URL path.</param>
    /// <param name="opened">The open that found the directory, carrying its timestamp.</param>
    /// <param name="lister">The lister to read the entry names from.</param>
    /// <returns>The outcome of the listing.</returns>
    private async ValueTask<TransferResult> ListDirectoryAsync(
        ITransferContext context,
        FileUrlPath path,
        FileOpenResult opened,
        IDirectoryLister lister)
    {
        long connectionNumber = connectionNumbers.NumberNextConnection();
        context.Progress.ReportTransferStarted();

        SourceLastWrite lastWrite = LastWriteOf(opened);
        KeyValuePair<string, string>[] headers = FileTransferMessages.DirectoryPseudoHeaders(lastWrite.HeaderTime);
        TransferResult result = await StartBodyAsync(context, lastWrite, headers).ConfigureAwait(false)
            ?? WithPseudoHeaders(
                context.NoBody
                    ? TransferResult.Success(0)
                    : await WriteEntryNamesAsync(context, path, lister).ConfigureAwait(false),
                headers);

        return EndDownload(context, result, connectionNumber, lastWrite);
    }

    /// <summary>
    /// Writes each entry name that does not start with <c>.</c>, then <c>\n</c>, as two
    /// body writes, in the lister's order, stopping at <c>--max-filesize</c> as any body
    /// write does (curl's <c>cw_download_write</c> writes what fits, then fails).
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="path">The parsed URL path.</param>
    /// <param name="lister">The lister to read the entry names from.</param>
    /// <returns>
    /// A success carrying the bytes written; exit 26 when the directory cannot be listed;
    /// exit 23 when the output refused a write; exit 63 at <c>--max-filesize</c>.
    /// </returns>
    private static async ValueTask<TransferResult> WriteEntryNamesAsync(
        ITransferContext context,
        FileUrlPath path,
        IDirectoryLister lister)
    {
        if (await lister.ListEntryNamesAsync(path.OsPath, context.CancellationToken).ConfigureAwait(false) is not { } names)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, FileTransferMessages.DirectoryListingFailed);
        }

        long maxWritten = context.MaxFileSize is > 0 and long maxFileSize ? maxFileSize : long.MaxValue;
        long transferred = 0;

        foreach (byte[] piece in ListingWrites(names))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.Events.ReportDataReceived(piece);
            int allowed = (int)Math.Min(piece.Length, maxWritten - transferred);

            if (await TryWriteInSlicesAsync(context.Output, piece.AsMemory(0, allowed), ChunkSize, context.CancellationToken)
                    .ConfigureAwait(false) is { } failure)
            {
                return TransferResult.Failure(
                    CurlExitCode.WriteError,
                    FileTransferMessages.OutputWriteFailed(failure.Offered, failure.Accepted),
                    transferred + failure.WrittenBefore);
            }

            transferred += allowed;

            if (allowed < piece.Length)
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
    /// The body writes of a directory listing: each entry name that does not start with
    /// <c>.</c>, in UTF-8, followed by a write of <c>\n</c>.
    /// </summary>
    /// <param name="names">Every entry name, in the order the lister gave them.</param>
    /// <returns>The writes, in order.</returns>
    private static IEnumerable<byte[]> ListingWrites(IReadOnlyList<string> names)
    {
        foreach (string name in names.Where(name => !name.StartsWith('.')))
        {
            yield return Encoding.UTF8.GetBytes(name);
            yield return FileTransferMessages.DirectoryEntrySeparator;
        }
    }

    /// <summary>
    /// Applies the time condition, emits the headers, applies every other option that can
    /// stop the body, then moves it.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="source">The opened source, which this method does not dispose.</param>
    /// <param name="opened">The metadata that came with the open.</param>
    /// <param name="lastWrite">The source's last-write time.</param>
    /// <returns>The outcome of the download.</returns>
    private static async ValueTask<TransferResult> DownloadFromAsync(
        ITransferContext context,
        Stream source,
        FileOpenResult opened,
        SourceLastWrite lastWrite)
    {
        KeyValuePair<string, string>[] headers = FileTransferMessages.PseudoHeaders(opened.Length, lastWrite.HeaderTime);

        if (await StartBodyAsync(context, lastWrite, headers).ConfigureAwait(false) is { } ended)
        {
            return ended;
        }

        TransferResult result = context.NoBody
            ? TransferResult.Success(0)
            : await DownloadBodyAsync(context, source, opened.Length).ConfigureAwait(false);

        return WithPseudoHeaders(result, headers);
    }

    /// <summary>
    /// Applies the time condition and writes the header block, the steps a file and a
    /// listed directory share before their bodies.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="lastWrite">The source's last-write time.</param>
    /// <param name="headers">The pseudo-headers to write.</param>
    /// <returns>
    /// <see langword="null" /> when the body follows; otherwise the unmet time condition's
    /// success or the failed header write.
    /// </returns>
    private static async ValueTask<TransferResult?> StartBodyAsync(
        ITransferContext context,
        SourceLastWrite lastWrite,
        KeyValuePair<string, string>[] headers)
    {
        if (!HasRange(context) && !MeetsTimeCondition(context.TimeCondition, lastWrite.UnixSeconds))
        {
            context.Events.ReportInfo(FileTransferMessages.TimeConditionNotMet(context.TimeCondition!.Kind));
            return TransferResult.TimeConditionNotMet();
        }

        return await WriteHeadersAsync(context, FileTransferMessages.HeaderLines(headers)).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports the pseudo-headers the transfer produced, so <c>%{num_headers}</c> counts
    /// them. curl 8.21.0 counts them once they were produced, whether or not <c>-D</c>
    /// wrote them anywhere and whatever the body did after, as measured in BL-285.
    /// </summary>
    /// <param name="result">The outcome of the body stage.</param>
    /// <param name="headers">The pseudo-headers the transfer produced.</param>
    /// <returns>
    /// <paramref name="result" /> with a report of the pseudo-headers and, because a report
    /// replaces <see cref="TransferResult.BytesTransferred" /> as the source of
    /// <c>%{size_download}</c>, its byte count.
    /// </returns>
    private static TransferResult WithPseudoHeaders(TransferResult result, KeyValuePair<string, string>[] headers) =>
        result with
        {
            Report = new TransferReport
            {
                PseudoHeaders = headers,
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
        // curl 8.21.0's file_do calls Curl_range only after the open and the -i header
        // block, and never under -I, so text that names no range fails here and no
        // earlier: a range only RangeText carries is one no parser could read.
        if (context.Range is null && context.RangeText is not null)
        {
            return ValueTask.FromResult(
                TransferResult.Failure(CurlExitCode.RangeError, FileTransferMessages.RangeNotDelivered));
        }

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
            DownloadReadSize,
            DownloadReadSize,
            ChunkSize,
            count,
            context.MaxFileSize is > 0 and long maxFileSize ? maxFileSize : long.MaxValue,
            chunk => context.Events.ReportDataReceived(chunk.Span),
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
    /// Numbers the upload's connection, performs the upload, then reports how it ended. An
    /// upload takes its number before its destination opens: curl 8.21.0 opens the
    /// destination in the transfer phase, so even a destination that will not open (exit 23)
    /// ends <c>* closing connection #0</c>, measured in BL-936.
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
        long connectionNumber = connectionNumbers.NumberNextConnection();
        TransferResult result = await UploadIntoDestinationAsync(context, path, upload).ConfigureAwait(false);

        return ReportConnectionEnd(context, result, connectionNumber);
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
    private async ValueTask<TransferResult> UploadIntoDestinationAsync(
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

        var transferLog = new FileTransferLog(context.DiagnosticLog);
        if (!opened.IsOpen || opened.Content is null)
        {
            transferLog.OpenFailed(path.OsPath, "writing", opened);
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                FileTransferMessages.CannotOpenForWriting(path.OsPath));
        }

        transferLog.OpenedForWriting(path.OsPath, mode);

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
        long? needed = RemainingLength(upload);
        long skip = context.ResumeFrom.GetValueOrDefault();

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

        Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> convertChunk = CreateUploadChunkConverter(context);

        return await CopyAsync(
                upload,
                destination,
                UploadChunkSize,
                firstChunkSize,
                UploadChunkSize * 2,
                long.MaxValue,
                long.MaxValue,
                static _ => { },
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
    /// Measures how many bytes an upload source has left to give.
    /// </summary>
    /// <param name="upload">The stream to upload from.</param>
    /// <returns>
    /// The bytes from its position to its end, or <see langword="null" /> when it cannot
    /// seek, as standard input cannot, so its length is unknown.
    /// </returns>
    private static long? RemainingLength(Stream upload) =>
        upload.CanSeek ? upload.Length - upload.Position : null;

    /// <summary>
    /// Picks what each upload chunk goes through on its way to the destination.
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <returns>
    /// A fresh <c>--crlf</c> converter when <see cref="ITransferContext.ConvertLineEndings" />
    /// is set, fresh per upload so the carriage return it remembers never leaks from one
    /// transfer into the next (bytes skipped by <c>-C</c> are not seen by it); otherwise
    /// a pass-through.
    /// </returns>
    private static Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> CreateUploadChunkConverter(ITransferContext context) =>
        context.ConvertLineEndings
            ? new CrlfUploadConverter(UploadChunkSize).Convert
            : static chunk => chunk;

    /// <summary>
    /// Moves up to <paramref name="count" /> bytes in <paramref name="chunkSize" /> chunks,
    /// telling a failed read apart from a failed write.
    /// </summary>
    /// <param name="source">Where the bytes come from.</param>
    /// <param name="destination">Where they go.</param>
    /// <param name="chunkSize">
    /// The most to read at once: <see cref="DownloadReadSize" /> for a download,
    /// <see cref="UploadChunkSize" /> for an upload.
    /// </param>
    /// <param name="firstChunkSize">
    /// The most to read the first time, which is <paramref name="chunkSize" /> unless an
    /// upload's <c>-C</c> skip left the source part-way into a chunk.
    /// </param>
    /// <param name="writeSize">
    /// The most to write at once: <see cref="ChunkSize" /> for a download, whose reads are
    /// larger; for an upload, twice <see cref="UploadChunkSize" />, so even a chunk doubled by
    /// <c>--crlf</c> is written whole.
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
    /// <param name="reportChunkRead">
    /// Told each chunk as read, before any of it is written or cut short by
    /// <paramref name="maxWritten" />: a download reports it as received data, as curl
    /// 8.21.0's <c>--trace</c> shows all six bytes of a file cut to three by
    /// <c>--max-filesize 3</c> (BL-936). An upload reports nothing, as curl writes no
    /// <c>=&gt; Send data</c> for a <c>file://</c> upload.
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
    /// Builds the outcome of a failed write, from the size of the write offered, how many
    /// of its bytes the destination accepted before failing, and the bytes written before
    /// that write.
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
        int writeSize,
        long count,
        long maxWritten,
        Action<ReadOnlyMemory<byte>> reportChunkRead,
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

            reportChunkRead(buffer.AsMemory(0, read));
            ReadOnlyMemory<byte> chunk = convertChunk(buffer.AsMemory(0, read));
            int allowed = (int)Math.Min(chunk.Length, maxWritten - transferred);

            // At the limit exactly, the next chunk writes nothing at all, not an empty write.
            if (await TryWriteInSlicesAsync(destination, chunk[..allowed], writeSize, cancellationToken)
                    .ConfigureAwait(false) is { } failure)
            {
                return reportWriteFailure(failure.Offered, failure.Accepted, transferred + failure.WrittenBefore);
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
    /// Writes a chunk in writes of at most <paramref name="writeSize" /> bytes, stopping at
    /// the first that fails. An empty chunk makes no write at all.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="chunk">The bytes to write.</param>
    /// <param name="writeSize">The most to write at once.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>
    /// <see langword="null" /> when every write succeeded; otherwise the size of the write
    /// that failed, how many of its bytes the destination accepted, and how many bytes of
    /// <paramref name="chunk" /> earlier writes had already delivered.
    /// </returns>
    private static async ValueTask<(int Offered, int Accepted, int WrittenBefore)?> TryWriteInSlicesAsync(
        Stream destination,
        ReadOnlyMemory<byte> chunk,
        int writeSize,
        CancellationToken cancellationToken)
    {
        for (int written = 0; written < chunk.Length; written += writeSize)
        {
            ReadOnlyMemory<byte> slice = chunk.Slice(written, Math.Min(writeSize, chunk.Length - written));

            if (await TryWriteAsync(destination, slice, cancellationToken).ConfigureAwait(false) is { } accepted)
            {
                return (slice.Length, accepted, written);
            }
        }

        return null;
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
    /// one line per write as curl does. A failed write's message reaches
    /// <see cref="ITransferContext.Events" /> as an information line through
    /// <see cref="ReportConnectionEnd" />, as libcurl's <c>failf</c> does: curl 8.21.0 under
    /// <c>-v</c> prints <c>* client returned ERROR on write of 20 bytes</c> before
    /// <c>curl: (23)</c> (measured 2026-09-26, BL-111 Notes).
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <param name="lines">The header lines to write, the blank line last.</param>
    /// <returns>
    /// <see langword="null" /> when the headers were written or not asked for, otherwise
    /// the exit 23 failure, reporting the length of the line that failed as the bytes
    /// offered.
    /// </returns>
    private static async ValueTask<TransferResult?> WriteHeadersAsync(
        ITransferContext context,
        string[] lines)
    {
        if (context.HeaderOutput is not { } headerOutput)
        {
            return null;
        }

        foreach (string line in lines)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(line);

            if (await TryWriteAsync(headerOutput, bytes, context.CancellationToken).ConfigureAwait(false) is not null)
            {
                return TransferResult.Failure(
                    CurlExitCode.WriteError,
                    FileTransferMessages.HeaderWriteFailed(bytes.Length));
            }
        }

        return null;
    }

    /// <summary>
    /// Applies <c>-z</c>/<c>--time-cond</c> to the timestamp the open reported.
    /// </summary>
    /// <param name="condition">The condition, or <see langword="null" /> for none.</param>
    /// <param name="lastWriteUnixSeconds">
    /// The file's last-write time in whole Unix seconds, or <see langword="null" /> when the
    /// file system could not determine one.
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
        long? lastWriteUnixSeconds)
    {
        return condition is null
            || lastWriteUnixSeconds is not { } knownLastWriteUnixSeconds
            || MeetsKnownTimeCondition(condition, knownLastWriteUnixSeconds);
    }

    /// <summary>
    /// Tells whether the transfer asks for part of the file, which is when curl 8.21.0's
    /// <c>file_do</c> skips the time condition (<c>lib/file.c</c>: it checks
    /// <c>-z</c> only when <c>state.range</c> is unset): <c>-r</c> text, parsable or not,
    /// or a positive <c>-C</c> offset. Measured on 2026-10-03: <c>-r 0-0 -z</c> and
    /// <c>-C 1 -z</c> with an unmet date both transfer (BL-1388).
    /// </summary>
    /// <param name="context">The transfer being performed.</param>
    /// <returns><see langword="true" /> when the transfer has a range.</returns>
    private static bool HasRange(ITransferContext context) =>
        context.Range is not null || context.RangeText is not null || context.ResumeFrom is > 0;

    /// <summary>
    /// Compares a known file time with a time condition in whole Unix seconds, which reach
    /// past 9999 where <see cref="DateTimeOffset" /> stops.
    /// </summary>
    /// <param name="condition">The condition to apply.</param>
    /// <param name="fileSeconds">The file's last-modified time in whole Unix seconds.</param>
    /// <returns>
    /// <see langword="true" /> when the body should be transferred, including when either
    /// side is the Unix epoch, which curl reads as unknown.
    /// </returns>
    private static bool MeetsKnownTimeCondition(
        TimeCondition condition,
        long fileSeconds)
    {
        long conditionSeconds = condition.ValueUnixSeconds;

        if (fileSeconds == 0 || conditionSeconds == 0)
        {
            return true;
        }

        return condition.Kind == TimeConditionKind.IfModifiedSince
            ? fileSeconds > conditionSeconds
            : fileSeconds < conditionSeconds;
    }

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

        if (range.Kind == ByteRangeKind.Suffix)
        {
            return TryResolveSuffix(range.SuffixLength.GetValueOrDefault(), length, out start, out count, out errorMessage);
        }

        return TryResolveFromStart(range, length, out start, out count);
    }

    /// <summary>
    /// Turns a suffix range - <c>-r -N</c> - into a window: the last
    /// <paramref name="suffixLength" /> bytes, or the whole file when it holds fewer.
    /// </summary>
    /// <param name="suffixLength">How many trailing bytes were asked for.</param>
    /// <param name="length">The length of the opened file.</param>
    /// <param name="start">On success, the first byte position to send.</param>
    /// <param name="count">On success, how many bytes to send from there.</param>
    /// <param name="errorMessage">On failure, the exit 36 message to report.</param>
    /// <returns>
    /// <see langword="false" /> when more than one byte more than the file holds was asked for.
    /// </returns>
    private static bool TryResolveSuffix(
        long suffixLength,
        long length,
        out long start,
        out long count,
        out string errorMessage)
    {
        errorMessage = FileTransferMessages.ResumeFailed;
        start = 0;
        count = 0;

        if (suffixLength > length + 1)
        {
            errorMessage = FileTransferMessages.CouldNotResumeDownload;

            return false;
        }

        start = Math.Max(0, length - suffixLength);
        count = length - start;

        return true;
    }

    /// <summary>
    /// Turns a <c>first-last</c> or <c>first-</c> range into a window, clamped to the file.
    /// </summary>
    /// <param name="range">The requested range, not a suffix.</param>
    /// <param name="length">The length of the opened file.</param>
    /// <param name="start">On success, the first byte position to send.</param>
    /// <param name="count">On success, how many bytes to send from there.</param>
    /// <returns>
    /// <see langword="false" /> when the first byte position is strictly past the end of the file.
    /// </returns>
    private static bool TryResolveFromStart(
        ByteRange range,
        long length,
        out long start,
        out long count)
    {
        count = 0;
        start = range.FirstBytePosition.GetValueOrDefault();

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
        // On an empty file the clamp lands on -1 and the count on 0. ByteRange keeps the end
        // at or past the start, and the start is at most the length here, so the count is
        // never negative; Math.Max states that floor without a branch no input can reach.
        count = Math.Max(
            0,
            range.LastBytePosition is { } lastBytePosition
                ? (Math.Min(lastBytePosition, length - 1) - start) + 1
                : length - start);

        return true;
    }
}
