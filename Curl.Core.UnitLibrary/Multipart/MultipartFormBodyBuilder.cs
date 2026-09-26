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
/// sent in the text encoding the builder is given. See ADR-0025.
/// </para>
/// </remarks>
/// <param name="fileSystem">Opens the files of file parts.</param>
/// <param name="textEncoding">
/// How names, file names, headers and text values become bytes: the platform curl's, which is
/// the ANSI code page on Windows and UTF-8 on Linux and macOS, as ADR-0022 measured for
/// credentials and ADR-0025 for form fields.
/// </param>
/// <param name="createBoundary">
/// Makes each multipart's boundary, the outermost first and then each nested one in the
/// order it is reached; production passes <see cref="MultipartBoundary.CreateRandom" />.
/// </param>
public sealed class MultipartFormBodyBuilder(IFileSystem fileSystem, Encoding textEncoding, Func<string> createBoundary)
{
    /// <summary>The message curl 8.21.0 gives, with exit 26, for a form file it cannot open.</summary>
    public const string OpenFailedMessage = "Failed to open/read local data from file/application";

    /// <summary>The message curl 8.21.0 gives, with exit 26, for a form file that is a directory.</summary>
    public const string ReadFailedMessage = "read error getting mime data";

    private const string FormDataDisposition = "form-data";

    /// <summary>Builds the body for <paramref name="parts" />.</summary>
    /// <param name="parts">The form's parts, in the order given on the command line.</param>
    /// <param name="cancellationToken">Cancels the file opens.</param>
    /// <returns>
    /// The body, with <c>Content-Type: multipart/form-data; boundary=&lt;boundary&gt;</c>; or
    /// exit 26, <see cref="CurlExitCode.ReadError" />, when a file cannot be opened.
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
            segments.AddText(MultipartPartHeaders.Format(part, disposition, boundary, out string? contentType));
            string? partsDisposition = MultipartPartHeaders.IsContentType(contentType, "multipart/form-data") ? FormDataDisposition : null;
            return await AddPartsAsync(segments, part.Parts, boundary, partsDisposition, cancellationToken).ConfigureAwait(false);
        }

        if (part.Kind == MultipartFormPartKind.Text)
        {
            segments.AddText(MultipartPartHeaders.Format(part, disposition, null, out _));
            segments.AddText(part.Content);
            return null;
        }

        return await AddFilePartAsync(segments, part, disposition, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult?> AddFilePartAsync(
        MultipartBodySegments segments,
        MultipartFormPart part,
        string? disposition,
        CancellationToken cancellationToken)
    {
        FileOpenResult opened = await fileSystem.OpenForReadAsync(part.Content, cancellationToken).ConfigureAwait(false);
        if (!opened.IsOpen)
        {
            string message = opened.Status == FileAccessStatus.IsDirectory ? ReadFailedMessage : OpenFailedMessage;
            return TransferResult.Failure(CurlExitCode.ReadError, message);
        }

        Stream content = opened.Content!;
        segments.AddText(MultipartPartHeaders.Format(part, disposition, null, out _));
        segments.AddStream(content, content.CanSeek ? opened.Length : null);
        return null;
    }
}
