using System.Text;
using System.Text.Unicode;
using Curl.Api.Options.Http;
using Curl.Core.Multipart;

namespace Curl.Engine;

/// <summary>
/// Maps the multipart body of <see cref="EasyHttpOptions.MimePost" />, which <c>Curl.Console</c>'s
/// <c>EasyOptionsFromCommandLine.HttpOf</c> builds from the <c>-F</c> / <c>--form</c> and
/// <c>--form-string</c> parts as curl's tool builds its <c>curl_mime</c> tree, onto the
/// <see cref="MultipartFormPart" /> <see cref="MultipartFormBodyBuilder" /> takes (ADR-0531).
/// </summary>
/// <remarks>
/// A part with <see cref="EasyMimePart.SubParts" /> is a multipart; <see cref="EasyMimeBytes" /> holding valid
/// UTF-8 is text, read as UTF-8, so the builder sends it in its text encoding - the same bytes for the engine's
/// UTF-8 builder, the platform's for the curl tool's <c>-F</c> text, which is command-line text - and any other
/// bytes are a <see cref="MultipartFormPartKind.Bytes" /> part, sent byte for byte (BL-2470); an <see cref="EasyMimeCallback" />
/// is a <see cref="MultipartFormPartKind.Reader" /> part over a <see cref="MimeCallbackReadStream" />, of
/// unknown length when its size is -1; an <see cref="EasyMimeFile" /> or <see cref="EasyMimeStandardInput" />
/// with a file name is a file upload, and one whose name was cleared, as for <c>name=&lt;file</c>, is file
/// content; a part with no data is empty text. Every other field is copied as it is,
/// <see cref="EasyMimePart.Encoder" /> included: the builder applies <c>;encoder=</c> and rejects an unknown
/// name (ADR-0041).
/// </remarks>
internal static class MultipartFormPartMapping
{
    private const string ContentTypeName = "Content-Type";

    /// <summary>Maps the parts of <paramref name="body" />, and the parts nested inside them, in order.</summary>
    /// <param name="body">The multipart body, from <see cref="EasyHttpOptions.MimePost" />.</param>
    /// <param name="callbackReads">
    /// Receives each <see cref="MimeCallbackReadStream" /> a callback part opens as the body is built, so the perform
    /// can tell that a read callback aborted it; <see langword="null" /> keeps none.
    /// </param>
    /// <param name="pauseGate">The handle's pause state each callback part's stream waits on after <c>CURL_READFUNC_PAUSE</c>, or <see langword="null" /> when the transfer cannot pause.</param>
    /// <returns>The builder's parts, one for each of the body's parts.</returns>
    internal static IReadOnlyList<MultipartFormPart> FromMimeBody(EasyMimeBody body, ICollection<CallbackReadStream>? callbackReads = null, EasyPauseGate? pauseGate = null) =>
        [.. body.Parts.Select(part => FromMimePart(part, callbackReads, pauseGate))];

    /// <summary>
    /// The escaping the builder gives names and file names: <see cref="MultipartNameEscaping.Backslash" />
    /// under <c>--form-escape</c> or for a MIME mail message to an <c>smtp</c>, <c>smtps</c>, <c>imap</c>
    /// or <c>imaps</c> URL, as libcurl 8.21.0's <c>escape_string</c> picks its table for the mail
    /// strategy (upstream test1187, BL-2091), otherwise curl's default
    /// <see cref="MultipartNameEscaping.Percent" /> (BL-625).
    /// </summary>
    /// <param name="formEscape">Whether <c>--form-escape</c> was given.</param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <returns>The escaping <paramref name="formEscape" /> and <paramref name="scheme" /> ask for.</returns>
    internal static MultipartNameEscaping NameEscapingOf(bool formEscape, string scheme) =>
        formEscape || MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme)
            ? MultipartNameEscaping.Backslash
            : MultipartNameEscaping.Percent;

    /// <summary>
    /// The request <c>Content-Type</c> the builder chooses the parts' disposition by, read as
    /// libcurl 8.21.0's <c>Curl_checkheaders</c> reads it: the first <c>-H</c> value naming
    /// <c>Content-Type</c> before a <c>:</c> or <c>;</c>, after that separator and its leading
    /// spaces, so <c>-H "Content-Type:"</c> gives an empty one (BL-1972).
    /// </summary>
    /// <param name="headers">The <c>-H</c> values of the option group whose form is being built.</param>
    /// <returns>The value, or <see langword="null" /> when no <c>-H</c> value names <c>Content-Type</c>.</returns>
    internal static string? RequestContentTypeOf(IEnumerable<string> headers) =>
        headers
            .Where(header => header.Length > ContentTypeName.Length
                && header[ContentTypeName.Length] is ':' or ';'
                && header.StartsWith(ContentTypeName, StringComparison.OrdinalIgnoreCase))
            .Select(header => header[(ContentTypeName.Length + 1)..].TrimStart(' '))
            .FirstOrDefault();

    private static MultipartFormPart FromMimePart(EasyMimePart part, ICollection<CallbackReadStream>? callbackReads, EasyPauseGate? pauseGate) =>
        new(
            part.Name,
            KindOf(part),
            ContentOf(part.Data),
            part.Type,
            part.FileName,
            part.Headers,
            part.SubParts is { } subParts ? FromMimeBody(subParts, callbackReads, pauseGate) : [])
        {
            Encoder = part.Encoder,
            Bytes = RawBytesOf(part.Data),
            OpenContent = OpenerOf(part.Data, callbackReads, pauseGate),
            ContentLength = ContentLengthOf(part.Data),
        };

    private static MultipartFormPartKind KindOf(EasyMimePart part) =>
        part switch
        {
            { SubParts: not null } => MultipartFormPartKind.Multipart,
            { Data: EasyMimeFile or EasyMimeStandardInput } => part.FileName is null ? MultipartFormPartKind.FileContent : MultipartFormPartKind.FileUpload,
            _ => KindOfData(part.Data),
        };

    /// <summary>The kind of a part with no sub-parts and no file: bytes that are not UTF-8, a callback, else text.</summary>
    private static MultipartFormPartKind KindOfData(EasyMimeData? data) =>
        data switch
        {
            EasyMimeBytes bytes when !Utf8.IsValid(bytes.Bytes.Span) => MultipartFormPartKind.Bytes,
            EasyMimeCallback => MultipartFormPartKind.Reader,
            _ => MultipartFormPartKind.Text,
        };

    /// <summary>The bytes of a <see cref="MultipartFormPartKind.Bytes" /> part: bytes that are not UTF-8, else none.</summary>
    private static ReadOnlyMemory<byte> RawBytesOf(EasyMimeData? data) =>
        data is EasyMimeBytes bytes && !Utf8.IsValid(bytes.Bytes.Span) ? bytes.Bytes : ReadOnlyMemory<byte>.Empty;

    /// <summary>What opens a callback part's stream, or <see langword="null" /> for any other data.</summary>
    private static Func<Stream>? OpenerOf(EasyMimeData? data, ICollection<CallbackReadStream>? callbackReads, EasyPauseGate? pauseGate) =>
        data is EasyMimeCallback callback ? () => OpenCallback(callback, callbackReads, pauseGate) : null;

    /// <summary>A callback part's size, or <see langword="null" /> when it is -1 (unknown) or the data is not a callback.</summary>
    private static long? ContentLengthOf(EasyMimeData? data) =>
        data is EasyMimeCallback { Size: >= 0 } sized ? sized.Size : null;

    private static string ContentOf(EasyMimeData? data) =>
        data switch
        {
            EasyMimeBytes bytes when Utf8.IsValid(bytes.Bytes.Span) => Encoding.UTF8.GetString(bytes.Bytes.Span),
            EasyMimeFile file => file.Path,
            EasyMimeStandardInput => MultipartFormPart.StandardInputPath,
            _ => string.Empty,
        };

    /// <summary>Opens <paramref name="callback" />'s stream, keeping it in <paramref name="callbackReads" /> when given.</summary>
    private static MimeCallbackReadStream OpenCallback(EasyMimeCallback callback, ICollection<CallbackReadStream>? callbackReads, EasyPauseGate? pauseGate)
    {
        MimeCallbackReadStream stream = new(callback, pauseGate);
        callbackReads?.Add(stream);
        return stream;
    }
}
