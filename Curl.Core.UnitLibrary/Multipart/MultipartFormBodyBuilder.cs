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
/// pipe or a device) makes the length unknown, and the body is sent chunked, as curl sends it.
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
/// describes. A file part whose encoder changes or checks its bytes is read whole while
/// building; one sent <c>binary</c> or <c>8bit</c> is streamed like any other. See ADR-0041.
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
public sealed class MultipartFormBodyBuilder(
    IFileSystem fileSystem,
    Encoding textEncoding,
    Func<string> createBoundary,
    Stream? standardInput = null)
{
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
    public async ValueTask<MultipartFormBuildResult> BuildAsync(
        IReadOnlyList<MultipartFormPart> parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);

        using MultipartBodySegments segments = new(textEncoding);
        string boundary = createBoundary();
        TransferResult? failure = await AddPartsAsync(segments, parts, boundary, FormDataDisposition, cancellationToken)
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
        StreamBody body = new(content, segments.Length, $"multipart/form-data; boundary={boundary}");
        return new MultipartFormBuildResult(body, null);
    }

    private async ValueTask<TransferResult?> AddPartsAsync(
        MultipartBodySegments segments,
        IReadOnlyList<MultipartFormPart> parts,
        string boundary,
        string? disposition,
        CancellationToken cancellationToken)
    {
        string lineBefore = string.Empty;
        foreach (MultipartFormPart part in parts)
        {
            segments.AddText($"{lineBefore}--{boundary}\r\n");
            TransferResult? failure = await AddPartAsync(segments, part, disposition, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        if (part.Kind == MultipartFormPartKind.Multipart)
        {
            string boundary = createBoundary();
            segments.AddText(MultipartPartHeaders.Format(part, disposition, boundary, null, out string? contentType));
            string? partsDisposition = MultipartPartHeaders.IsContentType(contentType, "multipart/form-data") ? FormDataDisposition : null;
            return await AddPartsAsync(segments, part.Parts, boundary, partsDisposition, cancellationToken).ConfigureAwait(false);
        }

        if (!TryFindEncoder(part.Encoder, out MultipartPartEncoder? encoder))
        {
            return TransferResult.Failure(CurlExitCode.BadFunctionArgument, UnknownEncoderMessage);
        }

        if (part.Kind == MultipartFormPartKind.Text)
        {
            AddTextPart(segments, part, disposition, encoder);
            return null;
        }

        return await AddFilePartAsync(segments, part, disposition, encoder, cancellationToken).ConfigureAwait(false);
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
        MultipartPartEncoder? encoder,
        CancellationToken cancellationToken) =>
        part.ReadsStandardInput && standardInput is not null
            ? AddStandardInputPartAsync(segments, part, disposition, encoder, standardInput, cancellationToken)
            : AddOpenedFilePartAsync(segments, part, disposition, encoder, cancellationToken);

    private async ValueTask<TransferResult?> AddOpenedFilePartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
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
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, out _));
        return await AddFileDataAsync(segments, content, content.CanSeek ? opened.Length : null, encoder, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Streams <paramref name="content" /> when its encoder sends it as it is, otherwise encodes it whole.</summary>
    private static ValueTask<TransferResult?> AddFileDataAsync(
        MultipartBodySegments segments,
        Stream content,
        long? length,
        MultipartPartEncoder? encoder,
        CancellationToken cancellationToken)
    {
        if (encoder is null || encoder.PassesDataThrough)
        {
            segments.AddStream(content, length);
            return ValueTask.FromResult<TransferResult?>(null);
        }

        return AddEncodedFileAsync(segments, content, length.HasValue, encoder, cancellationToken);
    }

    /// <summary>Reads <paramref name="content" /> whole, closes it, and adds its encoded bytes.</summary>
    private static async ValueTask<TransferResult?> AddEncodedFileAsync(
        MultipartBodySegments segments,
        Stream content,
        bool dataLengthIsKnown,
        MultipartPartEncoder encoder,
        CancellationToken cancellationToken)
    {
        await using (content.ConfigureAwait(false))
        {
            try
            {
                using MemoryStream copy = new();
                await content.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
                AddEncodedData(segments, copy.ToArray(), dataLengthIsKnown, encoder);
                return null;
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.ReadError, ReadFailedMessage);
            }
        }
    }

    /// <summary>Reads <paramref name="input" /> whole and adds it, encoded when the part names an encoder.</summary>
    private static async ValueTask<TransferResult?> AddStandardInputPartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
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

        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, out _));
        AddData(segments, copy.ToArray(), encoder);
        return null;
    }

    private void AddTextPart(MultipartBodySegments segments, MultipartFormPart part, string? disposition, MultipartPartEncoder? encoder)
    {
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, encoder?.Name, out _));
        if (encoder is null)
        {
            segments.AddText(part.Content);
            return;
        }

        AddEncodedData(segments, textEncoding.GetBytes(part.Content), dataLengthIsKnown: true, encoder);
    }

    /// <summary>Adds <paramref name="data" />, whose length is known, encoded when there is an encoder.</summary>
    private static void AddData(MultipartBodySegments segments, byte[] data, MultipartPartEncoder? encoder)
    {
        if (encoder is null)
        {
            segments.AddStream(new MemoryStream(data, writable: false), data.Length);
            return;
        }

        AddEncodedData(segments, data, dataLengthIsKnown: true, encoder);
    }

    private static void AddEncodedData(MultipartBodySegments segments, byte[] data, bool dataLengthIsKnown, MultipartPartEncoder encoder)
    {
        byte[]? encoded = encoder.Encode(data);
        if (encoded is null)
        {
            segments.AddRefusedData();
            return;
        }

        long? length = encoder.KnowsEncodedLength(dataLengthIsKnown ? data.Length : null) ? encoded.Length : null;
        segments.AddStream(new MemoryStream(encoded, writable: false), length);
    }
}
