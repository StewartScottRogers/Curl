using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

/// <summary>
/// Turns the parts <c>-F</c>/<c>--form</c> and <c>--form-string</c> describe into the
/// <c>multipart/form-data</c> <see cref="StreamBody" /> curl 8.21.0 sends, byte for byte.
/// </summary>
/// <remarks>
/// <para>
/// Every file is opened through <see cref="IFileSystem" /> while building, so the body's
/// <c>Content-Length</c> is known before anything is sent and a file that cannot be opened
/// fails the transfer before a connection is made, as curl's <c>stat</c> of each file does.
/// The files are then streamed, not read into memory. A file whose stream cannot seek (a
/// pipe or a device) declares the length curl's <c>stat</c> gives it, as
/// <see cref="UnseekableFileLength" /> describes: on Windows a fixed length the sender cuts the
/// body off at, whatever the pipe delivers; elsewhere none, so the body is sent chunked. A file
/// that can seek declares its length, except a device such as <c>/dev/null</c> off Windows, which
/// declares none, as <see cref="SeekableFileLength" /> describes.
/// </para>
/// <para>
/// The layout is libcurl 8.21.0's <c>lib/mime.c</c>: each part is preceded by
/// <c>--&lt;boundary&gt;</c> CRLF (with a CRLF before it for all but the first), the last is
/// followed by CRLF <c>--&lt;boundary&gt;--</c> CRLF, and each part's headers are chosen by
/// <see cref="MultipartPartHeaders" />. Names, file names, headers and text values are
/// sent in the text encoding the builder is given. See ADR-0027.
/// </para>
/// <para>
/// A part's <see cref="MultipartFormPart.Encoder" /> encodes its body as <see cref="MultipartPartEncoder" />
/// describes. A file part is streamed whatever its encoder, encoded as it is sent, never held
/// in memory whole. A <c>7bit</c> file that can seek is also read through once while building,
/// so a byte above 127 or a failed read fails the body before a connection is made. One that
/// cannot seek (a pipe) is not read while building: a byte above 127 fails the read that reaches
/// it with a <see cref="RequestBodyReadFailedException" />, which fails the send with exit 26, as
/// curl meets it. See ADR-0041, ADR-0076 and ADR-0093.
/// </para>
/// <para>
/// A file part whose path is <see cref="MultipartFormPart.StandardInputPath" /> reads standard
/// input whole while building, as curl 8.21.0's <c>tool_formparse.c</c> buffers it, so the
/// body keeps its <c>Content-Length</c>. Standard input is read where it stands and never
/// closed; a second such part gets whatever the first left, which after a whole read is
/// nothing, as curl sends it when standard input is a pipe.
/// </para>
/// </remarks>
/// <param name="fileSystem">Opens the files of file parts.</param>
/// <param name="textEncoding">
/// How names, file names, headers and text values become bytes: the platform curl's, which is
/// the ANSI code page on Windows and UTF-8 on Linux and macOS, as ADR-0022 measured for
/// credentials and ADR-0027 for form fields.
/// </param>
/// <param name="createBoundary">
/// Makes each multipart's boundary, the outermost first and then each nested one in the
/// order it is reached; production passes <see cref="MultipartBoundary.CreateRandom" />.
/// </param>
/// <param name="standardInput">
/// The stream <c>@-</c> and <c>&lt;-</c> parts read, which the caller owns; <see langword="null" />
/// leaves such a part opening the path <c>-</c> through <paramref name="fileSystem" />, as it did
/// before standard input could be given.
/// </param>
/// <param name="unseekableFileLength">
/// The length a file that cannot seek declares, from its path, or <see langword="null" /> for
/// none, which sends the body chunked; <see langword="null" /> takes the platform curl's rule,
/// <see cref="UnseekableFileLength.ForPlatform" />.
/// </param>
/// <param name="seekableFileLength">
/// The length a file that can seek declares, from its path and opened length, or
/// <see langword="null" /> for none, which sends the body chunked, as curl sends a device such as
/// <c>/dev/null</c> on Linux and macOS; <see langword="null" /> takes the platform curl's rule,
/// <see cref="SeekableFileLength.ForPlatform" />.
/// </param>
public sealed class MultipartFormBodyBuilder(
    IFileSystem fileSystem,
    Encoding textEncoding,
    Func<string> createBoundary,
    Stream? standardInput = null,
    Func<string, long?>? unseekableFileLength = null,
    Func<string, long, long?>? seekableFileLength = null)
{
    private readonly Func<string, long?> unseekableFileLength =
        unseekableFileLength ?? UnseekableFileLength.ForPlatform(OperatingSystem.IsWindows());

    private readonly Func<string, long, long?> seekableFileLength =
        seekableFileLength ?? SeekableFileLength.ForPlatform(OperatingSystem.IsWindows());

    /// <summary>
    /// Gets a value indicating whether a <c>7bit</c> part's refusal of a byte above 127 waits for
    /// the send, as libcurl 8.22.0 meets it (the headers and the bytes before the refused one go out,
    /// then the read fails, CURLcode 26; measured, BL-3210 case 2), instead of failing the build
    /// before connecting as the curl tool does.
    /// </summary>
    public bool SevenBitRefusalWaitsForSend { get; init; }

    /// <summary>
    /// Gets a value indicating whether a file part whose file does not open fails when the send reaches it, with
    /// exit 26 and no message, after the connection is made and the bytes before it are sent, as libcurl 8.22.0
    /// reads a missing <c>curl_formadd</c> <c>CURLFORM_FILE</c> (CGR-262), instead of failing the build before
    /// connecting as the curl tool does.
    /// </summary>
    public bool MissingFileWaitsForSend { get; init; }

    /// <summary>
    /// Gets a value indicating whether a directory part that is not sent empty (<see cref="SendsDirectoryEmpty" /> off, as on
    /// Windows) fails when the send reaches it, after the head and the part's headers are sent, with exit 26 and
    /// <see cref="ReadFailedMessage" />, as curl 8.22.0 sends it (CGR-198), instead of failing the build before connecting.
    /// </summary>
    public bool DirectoryReadFailsAtSend { get; init; }

    /// <summary>
    /// Gets a value indicating whether a file part naming a directory is sent as a part with its
    /// file name and no data, the body's length unknown (chunked), as curl 8.18.0 on Linux sends
    /// <c>-F f=@dir</c> and exits 0 (measured, CGR-069, BL-3896), instead of failing with exit 26,
    /// <see cref="ReadFailedMessage" />, as curl's Windows build does. Defaults to the platform
    /// curl's: <see langword="true" /> off Windows.
    /// </summary>
    public bool SendsDirectoryEmpty { get; init; } = !OperatingSystem.IsWindows();

    /// <summary>The message curl 8.21.0 gives, with exit 26, for a form file it cannot open.</summary>
    public const string OpenFailedMessage = "Failed to open/read local data from file/application";

    /// <summary>The message curl 8.21.0 gives, with exit 26, for a form file that is a directory.</summary>
    public const string ReadFailedMessage = "read error getting mime data";

    /// <summary>The message curl gives when the read of a closed standard input aborts the transfer, with exit 42.</summary>
    public const string AbortedByCallbackMessage = "operation aborted by callback";

    /// <summary>The message curl 8.21.0 gives, with exit 43, for an <c>;encoder=</c> it does not know.</summary>
    public const string UnknownEncoderMessage = "A libcurl function was given a bad argument";

    /// <summary>
    /// The message curl 8.22.0 gives, with exit 43, for a CR or LF in a mail part's name or file
    /// name, or in any part's under <c>--form-escape</c>: <c>curl_easy_strerror(CURLE_BAD_FUNCTION_ARGUMENT)</c>.
    /// </summary>
    public const string LineBreakInNameMessage = "A libcurl function was given a bad argument";

    private const string FormDataDisposition = "form-data";

    /// <summary>Builds the body for <paramref name="parts" />.</summary>
    /// <param name="parts">The form's parts, in the order given on the command line.</param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>
    /// The body, with <c>Content-Type: multipart/form-data; boundary=&lt;boundary&gt;</c>; exit
    /// 26, <see cref="CurlExitCode.ReadError" />, when a file cannot be opened or read or a
    /// <c>7bit</c> part holds a byte above 127; or exit 43, <see cref="CurlExitCode.BadFunctionArgument" />,
    /// when a part names an encoder curl does not have. The first part to fail in order decides,
    /// except that a <c>7bit</c> refusal waits for every other part, as curl meets it only while sending.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts" /> is <see langword="null" />.</exception>
    public ValueTask<MultipartFormBuildResult> BuildAsync(
        IReadOnlyList<MultipartFormPart> parts,
        CancellationToken cancellationToken) =>
        BuildAsync(parts, MultipartNameEscaping.Percent, cancellationToken);

    /// <summary>
    /// Builds the body for <paramref name="parts" />, escaping every part's name and file name
    /// as <paramref name="nameEscaping" /> says: <see cref="MultipartNameEscaping.Backslash" /> under
    /// <c>--form-escape</c>, otherwise curl's default <see cref="MultipartNameEscaping.Percent" />.
    /// </summary>
    /// <param name="parts">The form's parts, in the order given on the command line.</param>
    /// <param name="nameEscaping">How names and file names are escaped in <c>Content-Disposition</c>.</param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>The body or the failure, as <see cref="BuildAsync(IReadOnlyList{MultipartFormPart}, CancellationToken)" /> gives them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts" /> is <see langword="null" />.</exception>
    public ValueTask<MultipartFormBuildResult> BuildAsync(
        IReadOnlyList<MultipartFormPart> parts,
        MultipartNameEscaping nameEscaping,
        CancellationToken cancellationToken) =>
        BuildAsync(parts, nameEscaping, null, cancellationToken);

    /// <summary>
    /// Builds the body for <paramref name="parts" /> as the overload without
    /// <paramref name="requestContentType" /> does, except that the parts' disposition follows
    /// the request's <c>Content-Type</c> as libcurl 8.21.0's <c>Curl_mime_prepare_headers</c>
    /// chooses it: <c>form-data</c> when <paramref name="requestContentType" /> is
    /// <see langword="null" /> or <c>multipart/form-data</c>, otherwise each named part falls back
    /// to <c>attachment</c> (upstream test 277, BL-1972).
    /// </summary>
    /// <param name="parts">The form's parts, in the order given on the command line.</param>
    /// <param name="nameEscaping">How names and file names are escaped in <c>Content-Disposition</c>.</param>
    /// <param name="requestContentType">
    /// The value of the first <c>-H</c> header naming <c>Content-Type</c>, empty for
    /// <c>-H "Content-Type:"</c>; <see langword="null" /> when no <c>-H</c> header names it.
    /// </param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>The body or the failure, as <see cref="BuildAsync(IReadOnlyList{MultipartFormPart}, CancellationToken)" /> gives them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts" /> is <see langword="null" />.</exception>
    public async ValueTask<MultipartFormBuildResult> BuildAsync(
        IReadOnlyList<MultipartFormPart> parts,
        MultipartNameEscaping nameEscaping,
        string? requestContentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);

        string? disposition = requestContentType is null || MultipartPartHeaders.IsContentType(requestContentType, "multipart/form-data")
            ? FormDataDisposition
            : null;
        return await BuildBodyAsync(parts, "multipart/form-data", null, disposition, nameEscaping, false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the MIME mail message curl 8.21.0 sends for <c>-F</c> parts to an <c>smtp://</c> or
    /// <c>imap://</c> URL, as libcurl's <c>MIMESTRATEGY_MAIL</c> lays it out (measured, BL-1988):
    /// <c>Content-Type: multipart/mixed; boundary=&lt;boundary&gt;</c>, <c>Mime-Version: 1.0</c>
    /// and <paramref name="messageHeaders" /> head the message, a part falls back to
    /// <c>attachment</c> only for a name or a file name, a default <c>text/plain</c> goes
    /// unlabelled, a labelled part without an encoder is <c>Content-Transfer-Encoding: 8bit</c>,
    /// and a <c>7bit</c> file is never read ahead, so a byte above 127 fails the read that
    /// reaches it while the message is sent, as curl meets it after <c>DATA</c>.
    /// </summary>
    /// <param name="parts">The message's parts, in the order given on the command line.</param>
    /// <param name="nameEscaping">How names and file names are escaped in <c>Content-Disposition</c>.</param>
    /// <param name="messageHeaders">The <c>-H</c> headers, each sent as given, after <c>Mime-Version</c>.</param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>
    /// The message, its headers included, with the outermost multipart's content type; or the
    /// failure, as <see cref="BuildAsync(IReadOnlyList{MultipartFormPart}, CancellationToken)" /> gives it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts" /> or <paramref name="messageHeaders" /> is <see langword="null" />.</exception>
    public ValueTask<MultipartFormBuildResult> BuildMailMessageAsync(
        IReadOnlyList<MultipartFormPart> parts,
        MultipartNameEscaping nameEscaping,
        IReadOnlyList<string> messageHeaders,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(messageHeaders);
        return BuildBodyAsync(parts, "multipart/mixed", messageHeaders, null, nameEscaping, true, cancellationToken);
    }

    /// <summary>
    /// Builds the multipart body of <paramref name="parts" /> as <paramref name="contentType" />,
    /// preceded, for a mail message, by its message headers.
    /// </summary>
    private async ValueTask<MultipartFormBuildResult> BuildBodyAsync(
        IReadOnlyList<MultipartFormPart> parts,
        string contentType,
        IReadOnlyList<string>? messageHeaders,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        CancellationToken cancellationToken)
    {
        using MultipartBodySegments segments = new(textEncoding);
        string boundary = createBoundary();
        string labelledContentType = $"{contentType}; boundary={boundary}";
        if (messageHeaders is not null)
        {
            segments.AddText($"Content-Type: {labelledContentType}\r\nMime-Version: 1.0\r\n"
                + string.Concat(messageHeaders.Select(header => header + "\r\n")) + "\r\n");
        }

        TransferResult? failure = await AddPartsAsync(segments, parts, boundary, disposition, nameEscaping, forMail, cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null)
        {
            return new MultipartFormBuildResult(null, failure);
        }

        if (segments.HoldsRefusedData)
        {
            return new MultipartFormBuildResult(null, TransferResult.Failure(CurlExitCode.ReadError, ReadFailedMessage));
        }

        // A mail message of unknown size, as a quoted-printable part makes it, cannot seek, so
        // SMTP sends no SIZE= and IMAP refuses to APPEND it, as curl 8.21.0 does (BL-2025).
        Stream content = segments.ToStream(allowsSeeking: !forMail || segments.Length is not null);
        StreamBody body = new(content, segments.Length, labelledContentType);
        return new MultipartFormBuildResult(body, null);
    }

    private async ValueTask<TransferResult?> AddPartsAsync(
        MultipartBodySegments segments,
        IReadOnlyList<MultipartFormPart> parts,
        string boundary,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        CancellationToken cancellationToken)
    {
        string lineBefore = string.Empty;
        foreach (MultipartFormPart part in parts)
        {
            segments.AddText($"{lineBefore}--{boundary}\r\n");
            TransferResult? failure = RefuseLineBreakInName(part, nameEscaping, forMail)
                ?? await AddPartAsync(segments, part, disposition, nameEscaping, forMail, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            lineBefore = "\r\n";
        }

        segments.AddText($"{lineBefore}--{boundary}--\r\n");
        return null;
    }

    private async ValueTask<TransferResult?> AddPartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        CancellationToken cancellationToken)
    {
        if (part.Kind == MultipartFormPartKind.Multipart)
        {
            // An encoder libcurl does not have is ignored here, as it always was for a nested multipart.
            return await AddMultipartPartAsync(segments, part, disposition, nameEscaping, forMail, part.Encoder is null ? null : MultipartPartEncoder.Find(part.Encoder), cancellationToken).ConfigureAwait(false);
        }

        if (!TryFindEncoder(part.Encoder, out MultipartPartEncoder? encoder))
        {
            return TransferResult.Failure(CurlExitCode.BadFunctionArgument, UnknownEncoderMessage);
        }

        if (!ReadsAFile(part))
        {
            AddPartWithoutFile(segments, part, disposition, nameEscaping, forMail, encoder);
            return null;
        }

        return await AddFilePartAsync(segments, part, disposition, nameEscaping, forMail, encoder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a part with subparts: its headers, then the nested multipart block, which a part given an
    /// encoder through libcurl has encoded whole, headers and boundaries included, under
    /// <c>Content-Transfer-Encoding</c> (measured against libcurl 8.22.0, BL-3213).
    /// </summary>
    private async ValueTask<TransferResult?> AddMultipartPartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder,
        CancellationToken cancellationToken)
    {
        string boundary = createBoundary();
        segments.AddText(MultipartPartHeaders.Format(part, disposition, boundary, encoder?.Name, nameEscaping, forMail, out string? contentType));
        string? partsDisposition = MultipartPartHeaders.IsContentType(contentType, "multipart/form-data") ? FormDataDisposition : null;
        if (encoder is null || encoder.PassesDataThrough)
        {
            return await AddPartsAsync(segments, part.Parts, boundary, partsDisposition, nameEscaping, forMail, cancellationToken).ConfigureAwait(false);
        }

        return await AddEncodedPartsAsync(segments, part.Parts, boundary, partsDisposition, nameEscaping, forMail, encoder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Adds the nested multipart block of <paramref name="parts" />, built whole and encoded as it is sent.</summary>
    private async ValueTask<TransferResult?> AddEncodedPartsAsync(
        MultipartBodySegments segments,
        IReadOnlyList<MultipartFormPart> parts,
        string boundary,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder encoder,
        CancellationToken cancellationToken)
    {
        MultipartBodySegments nested = new(textEncoding);
        TransferResult? failure = await AddPartsAsync(nested, parts, boundary, disposition, nameEscaping, forMail, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            nested.Dispose();
            return failure;
        }

        if (nested.HoldsRefusedData)
        {
            segments.AddRefusedData();
        }

        ConcatenatedReadStream block = nested.ToStream();
        segments.AddStream(encoder.EncodeWhileReading(block, nested.Length), encoder.EncodedLength(nested.Length));
        return null;
    }
    /// <summary>
    /// Gives exit 43 for a part whose name or file name holds a CR or LF that its escaping cannot
    /// write, as <see cref="MultipartPartHeaders.RefusesLineBreakInName" /> tells; otherwise <see langword="null" />.
    /// </summary>
    private static TransferResult? RefuseLineBreakInName(MultipartFormPart part, MultipartNameEscaping nameEscaping, bool forMail) =>
        MultipartPartHeaders.RefusesLineBreakInName(part, nameEscaping, forMail)
            ? TransferResult.Failure(CurlExitCode.BadFunctionArgument, LineBreakInNameMessage)
            : null;

    /// <summary>Whether <paramref name="part" /> is a <see cref="MultipartFormPartKind.FileUpload" /> or <see cref="MultipartFormPartKind.FileContent" /> part.</summary>
    private static bool ReadsAFile(MultipartFormPart part) =>
        part.Kind is MultipartFormPartKind.FileUpload or MultipartFormPartKind.FileContent;

    /// <summary>Finds the encoder <paramref name="name" /> names; no name finds no encoder, which is not a failure.</summary>
    private static bool TryFindEncoder(string? name, out MultipartPartEncoder? encoder)
    {
        encoder = name is null ? null : MultipartPartEncoder.Find(name);
        return name is null || encoder is not null;
    }

    /// <summary>Adds a file part, read from standard input when its path is <c>-</c> and there is one, otherwise opened.</summary>
    private ValueTask<TransferResult?> AddFilePartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder,
        CancellationToken cancellationToken) =>
        part.ReadsStandardInput && standardInput is not null
            ? AddStandardInputPartAsync(segments, part, disposition, nameEscaping, forMail, encoder, standardInput, cancellationToken)
            : AddOpenedFilePartAsync(segments, part, disposition, nameEscaping, forMail, encoder, cancellationToken);

    private async ValueTask<TransferResult?> AddOpenedFilePartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder,
        CancellationToken cancellationToken)
    {
        FileOpenResult opened = await fileSystem.OpenForReadAsync(part.Content, cancellationToken).ConfigureAwait(false);
        if (!opened.IsOpen)
        {
            return await AddUnopenedFilePartAsync(segments, part, opened.Status, disposition, nameEscaping, forMail, encoder, cancellationToken)
                .ConfigureAwait(false);
        }

        Stream content = opened.Content!;
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        long? length = content.CanSeek ? seekableFileLength(part.Content, opened.Length) : unseekableFileLength(part.Content);
        return await AddFileDataAsync(segments, content, length, encoder, forMail, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a file that did not open: a directory under <see cref="SendsDirectoryEmpty" /> is sent with its file
    /// name and no data, its length unknown, as <c>fopen</c> opens a directory off Windows and its first read gives
    /// nothing (CGR-069); any other file is left to <see cref="OpenFailure" />.
    /// </summary>
    private ValueTask<TransferResult?> AddUnopenedFilePartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        FileAccessStatus status,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder,
        CancellationToken cancellationToken)
    {
        if (status != FileAccessStatus.IsDirectory || !SendsDirectoryEmpty)
        {
            return ValueTask.FromResult(OpenFailure(segments, part, status, disposition, nameEscaping, forMail, encoder));
        }

        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        return AddFileDataAsync(segments, Stream.Null, null, encoder, forMail, cancellationToken);
    }

    /// <summary>
    /// Handles a file that did not open: a part that may be sent empty (not a directory) adds an empty text part with no
    /// file name and fails nothing, as libcurl 8.22.0 sends a missing <c>curl_mime_filedata</c> file (BL-3211); any
    /// other part fails with exit 26.
    /// </summary>
    private TransferResult? OpenFailure(
        MultipartBodySegments segments,
        MultipartFormPart part,
        FileAccessStatus status,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder)
    {
        if (status == FileAccessStatus.IsDirectory)
        {
            if (!DirectoryReadFailsAtSend)
            {
                return TransferResult.Failure(CurlExitCode.ReadError, ReadFailedMessage);
            }

            segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
            segments.AddStream(new FailingReadStream(), null);
            return null;
        }

        if (!part.SendsEmptyWhenMissing)
        {
            return MissingFileFailure(segments, part, disposition, nameEscaping, forMail, encoder);
        }

        segments.AddText(MultipartPartHeaders.Format(part with { Kind = MultipartFormPartKind.Text, FileName = null }, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        AddData(segments, [], encoder, forMail);
        return null;
    }

    /// <summary>
    /// A missing file that may not be sent empty: exit 26 before anything is sent, or under
    /// <see cref="MissingFileWaitsForSend" /> the part's head followed by a read that fails with exit 26 and no message.
    /// </summary>
    private TransferResult? MissingFileFailure(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder)
    {
        if (!MissingFileWaitsForSend)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, OpenFailedMessage);
        }

        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        segments.AddStream(new UnreadableFileStream(), null);
        return null;
    }

    /// <summary>
    /// Streams <paramref name="content" />, encoded as it is sent when its encoder encodes; a
    /// <c>7bit</c> file that can seek back is checked first, and one that cannot is refused
    /// only when the send reaches the refused byte.
    /// </summary>
    private ValueTask<TransferResult?> AddFileDataAsync(
        MultipartBodySegments segments,
        Stream content,
        long? length,
        MultipartPartEncoder? encoder,
        bool forMail,
        CancellationToken cancellationToken)
    {
        if (encoder is null || encoder.PassesDataThrough)
        {
            segments.AddFileStream(content, length);
            return ValueTask.FromResult<TransferResult?>(null);
        }

        if (RefusesBeforeSending(encoder, content, forMail))
        {
            return AddCheckedFileAsync(segments, content, length, encoder, cancellationToken);
        }

        segments.AddStream(encoder.EncodeWhileReading(content, length), encoder.EncodedLength(length));
        return ValueTask.FromResult<TransferResult?>(null);
    }

    /// <summary>Whether <paramref name="encoder" />'s refusal of <paramref name="content" /> is found by reading it through before the send.</summary>
    private bool RefusesBeforeSending(MultipartPartEncoder encoder, Stream content, bool forMail) =>
        encoder.CanRefuseData && content.CanSeek && !forMail && !SevenBitRefusalWaitsForSend;

    /// <summary>
    /// Reads <paramref name="content" /> through its encoder once, keeping nothing, then adds it
    /// to be encoded again as it is sent; refused data waits to fail the body, and a failed read
    /// fails it now.
    /// </summary>
    private static async ValueTask<TransferResult?> AddCheckedFileAsync(
        MultipartBodySegments segments,
        Stream content,
        long? length,
        MultipartPartEncoder encoder,
        CancellationToken cancellationToken)
    {
        // The segments own the stream from here, so a body that fails closes it with the rest.
        EncodedReadStream encoded = encoder.EncodeWhileReading(content, length);
        segments.AddStream(encoded, encoder.EncodedLength(length));
        try
        {
            await encoded.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            encoded.Position = 0;
            return null;
        }
        catch (RequestBodyReadFailedException)
        {
            segments.AddRefusedData();
            return null;
        }
        catch (IOException)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, ReadFailedMessage);
        }
    }

    /// <summary>
    /// Reads <paramref name="input" /> whole and adds it, encoded when the part names an encoder;
    /// a <see cref="ClosedStandardInputStream" /> instead adds the part's headers and then a read
    /// that aborts the transfer, as curl 8.22.0's tool sends the part head chunked and ends with
    /// exit 42 when its <c>fread</c> on a closed <c>stdin</c> fails (CGR-110, ADR-0967).
    /// </summary>
    private async ValueTask<TransferResult?> AddStandardInputPartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder,
        Stream input,
        CancellationToken cancellationToken)
    {
        string head = MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _);
        if (input is ClosedStandardInputStream)
        {
            segments.AddText(head);
            segments.AddCallbackStream(new AbortedReadStream(), null);
            return null;
        }

        using MemoryStream copy = new();
        try
        {
            await input.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, ReadFailedMessage);
        }

        segments.AddText(head);
        AddData(segments, copy.ToArray(), encoder, forMail);
        return null;
    }

    /// <summary>
    /// Adds a <see cref="MultipartFormPartKind.Text" />, <see cref="MultipartFormPartKind.Bytes" /> or
    /// <see cref="MultipartFormPartKind.Reader" /> part: its headers, then its text, its bytes as they
    /// are, or the stream its <see cref="MultipartFormPart.OpenContent" /> opens.
    /// </summary>
    private void AddPartWithoutFile(MultipartBodySegments segments, MultipartFormPart part, string? disposition, MultipartNameEscaping nameEscaping, bool forMail, MultipartPartEncoder? encoder)
    {
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        switch (part.Kind)
        {
            case MultipartFormPartKind.Bytes:
                AddData(segments, part.Bytes.ToArray(), encoder, forMail);
                break;
            case MultipartFormPartKind.Reader:
                AddReaderData(segments, part.OpenContent?.Invoke() ?? Stream.Null, part.ContentLength, encoder);
                break;
            default:
                AddTextData(segments, part, encoder, forMail);
                break;
        }
    }

    /// <summary>
    /// Adds a reader part's stream, encoded as it is sent when its encoder encodes; it is never read
    /// ahead, so a <c>7bit</c> refusal fails the read that reaches it, as libcurl meets a callback part's.
    /// </summary>
    private static void AddReaderData(MultipartBodySegments segments, Stream content, long? length, MultipartPartEncoder? encoder)
    {
        if (encoder is null || encoder.PassesDataThrough)
        {
            segments.AddCallbackStream(content, length);
            return;
        }

        segments.AddCallbackStream(encoder.EncodeWhileReading(content, length), encoder.EncodedLength(length));
    }

    private void AddTextData(MultipartBodySegments segments, MultipartFormPart part, MultipartPartEncoder? encoder, bool forMail)
    {
        if (encoder is null)
        {
            segments.AddText(part.Content);
            return;
        }

        AddEncodedData(segments, textEncoding.GetBytes(part.Content), encoder, forMail);
    }

    /// <summary>Adds <paramref name="data" />, whose length is known, encoded when there is an encoder.</summary>
    private void AddData(MultipartBodySegments segments, byte[] data, MultipartPartEncoder? encoder, bool forMail)
    {
        if (encoder is null)
        {
            segments.AddStream(new MemoryStream(data, writable: false), data.Length);
            return;
        }

        AddEncodedData(segments, data, encoder, forMail);
    }

    /// <summary>
    /// Adds <paramref name="data" /> encoded; in a mail message a <c>7bit</c> part is encoded as
    /// it is sent, so the bytes before a byte above 127 go out after <c>DATA</c> and the read
    /// that reaches it fails, as curl 8.21.0 sends it (measured, BL-2025), while a form's
    /// refused data fails the body before it connects.
    /// </summary>
    private void AddEncodedData(MultipartBodySegments segments, byte[] data, MultipartPartEncoder encoder, bool forMail)
    {
        if ((forMail || SevenBitRefusalWaitsForSend) && encoder.CanRefuseData)
        {
            segments.AddStream(encoder.EncodeWhileReading(new MemoryStream(data, writable: false), data.Length), data.Length);
            return;
        }

        byte[]? encoded = encoder.Encode(data);
        if (encoded is null)
        {
            segments.AddRefusedData();
            return;
        }

        long? length = encoder.EncodedLength(data.Length);
        segments.AddStream(new MemoryStream(encoded, writable: false), length);
    }

    /// <summary>
    /// The part data of a closed standard input: every read throws a
    /// <see cref="RequestBodyReadFailedException" /> with exit 42,
    /// <see cref="CurlExitCode.AbortedByCallback" />, and curl's message for it. It derives from
    /// <see cref="MemoryStream" /> so every other read overload comes to this one.
    /// </summary>
    private sealed class UnreadableFileStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new RequestBodyReadFailedException(string.Empty);
    }

    private sealed class FailingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new RequestBodyReadFailedException(ReadFailedMessage);
    }

    private sealed class AbortedReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new RequestBodyReadFailedException(CurlExitCode.AbortedByCallback, AbortedByCallbackMessage);
    }
}
