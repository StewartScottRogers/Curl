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

    /// <summary>The message curl 8.21.0 gives, with exit 26, for a form file it cannot open.</summary>
    public const string OpenFailedMessage = "Failed to open/read local data from file/application";

    /// <summary>The message curl 8.21.0 gives, with exit 26, for a form file that is a directory.</summary>
    public const string ReadFailedMessage = "read error getting mime data";

    /// <summary>The message curl 8.21.0 gives, with exit 43, for an <c>;encoder=</c> it does not know.</summary>
    public const string UnknownEncoderMessage = "A libcurl function was given a bad argument";

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

        Stream content = segments.ToStream();
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
            TransferResult? failure = await AddPartAsync(segments, part, disposition, nameEscaping, forMail, cancellationToken).ConfigureAwait(false);
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
            string boundary = createBoundary();
            segments.AddText(MultipartPartHeaders.Format(part, disposition, boundary, null, nameEscaping, forMail, out string? contentType));
            string? partsDisposition = MultipartPartHeaders.IsContentType(contentType, "multipart/form-data") ? FormDataDisposition : null;
            return await AddPartsAsync(segments, part.Parts, boundary, partsDisposition, nameEscaping, forMail, cancellationToken).ConfigureAwait(false);
        }

        if (!TryFindEncoder(part.Encoder, out MultipartPartEncoder? encoder))
        {
            return TransferResult.Failure(CurlExitCode.BadFunctionArgument, UnknownEncoderMessage);
        }

        if (part.Kind == MultipartFormPartKind.Text)
        {
            AddTextPart(segments, part, disposition, nameEscaping, forMail, encoder);
            return null;
        }

        return await AddFilePartAsync(segments, part, disposition, nameEscaping, forMail, encoder, cancellationToken).ConfigureAwait(false);
    }

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
            string message = opened.Status == FileAccessStatus.IsDirectory ? ReadFailedMessage : OpenFailedMessage;
            return TransferResult.Failure(CurlExitCode.ReadError, message);
        }

        Stream content = opened.Content!;
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        long? length = content.CanSeek ? seekableFileLength(part.Content, opened.Length) : unseekableFileLength(part.Content);
        return await AddFileDataAsync(segments, content, length, encoder, forMail, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Streams <paramref name="content" />, encoded as it is sent when its encoder encodes; a
    /// <c>7bit</c> file that can seek back is checked first, and one that cannot is refused
    /// only when the send reaches the refused byte.
    /// </summary>
    private static ValueTask<TransferResult?> AddFileDataAsync(
        MultipartBodySegments segments,
        Stream content,
        long? length,
        MultipartPartEncoder? encoder,
        bool forMail,
        CancellationToken cancellationToken)
    {
        if (encoder is null || encoder.PassesDataThrough)
        {
            segments.AddStream(content, length);
            return ValueTask.FromResult<TransferResult?>(null);
        }

        if (encoder.CanRefuseData && content.CanSeek && !forMail)
        {
            return AddCheckedFileAsync(segments, content, length, encoder, cancellationToken);
        }

        segments.AddStream(encoder.EncodeWhileReading(content, length), encoder.EncodedLength(length));
        return ValueTask.FromResult<TransferResult?>(null);
    }

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

    /// <summary>Reads <paramref name="input" /> whole and adds it, encoded when the part names an encoder.</summary>
    private static async ValueTask<TransferResult?> AddStandardInputPartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        MultipartNameEscaping nameEscaping,
        bool forMail,
        MultipartPartEncoder? encoder,
        Stream input,
        CancellationToken cancellationToken)
    {
        using MemoryStream copy = new();
        try
        {
            await input.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return TransferResult.Failure(CurlExitCode.ReadError, ReadFailedMessage);
        }

        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        AddData(segments, copy.ToArray(), encoder);
        return null;
    }

    private void AddTextPart(MultipartBodySegments segments, MultipartFormPart part, string? disposition, MultipartNameEscaping nameEscaping, bool forMail, MultipartPartEncoder? encoder)
    {
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, nameEscaping, forMail, out _));
        if (encoder is null)
        {
            segments.AddText(part.Content);
            return;
        }

        AddEncodedData(segments, textEncoding.GetBytes(part.Content), encoder);
    }

    /// <summary>Adds <paramref name="data" />, whose length is known, encoded when there is an encoder.</summary>
    private static void AddData(MultipartBodySegments segments, byte[] data, MultipartPartEncoder? encoder)
    {
        if (encoder is null)
        {
            segments.AddStream(new MemoryStream(data, writable: false), data.Length);
            return;
        }

        AddEncodedData(segments, data, encoder);
    }

    private static void AddEncodedData(MultipartBodySegments segments, byte[] data, MultipartPartEncoder encoder)
    {
        byte[]? encoded = encoder.Encode(data);
        if (encoded is null)
        {
            segments.AddRefusedData();
            return;
        }

        long? length = encoder.EncodedLength(data.Length);
        segments.AddStream(new MemoryStream(encoded, writable: false), length);
    }
}
