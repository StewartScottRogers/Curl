using System.Text;
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
/// A part with <see cref="EasyMimePart.SubParts" /> is a multipart; <see cref="EasyMimeBytes" /> is
/// text, read as UTF-8; an <see cref="EasyMimeFile" /> or <see cref="EasyMimeStandardInput" /> with a
/// file name is a file upload, and one whose name was cleared, as for <c>name=&lt;file</c>, is file
/// content. Every other field is copied as it is, <see cref="EasyMimePart.Encoder" /> included: the
/// builder applies <c>;encoder=</c> and rejects an unknown name (ADR-0041).
/// </remarks>
internal static class MultipartFormPartMapping
{
    private const string ContentTypeName = "Content-Type";

    /// <summary>Maps the parts of <paramref name="body" />, and the parts nested inside them, in order.</summary>
    /// <param name="body">The multipart body, from <see cref="EasyHttpOptions.MimePost" />.</param>
    /// <returns>The builder's parts, one for each of the body's parts.</returns>
    /// <exception cref="NotSupportedException">
    /// A part's data is an <see cref="EasyMimeCallback" />, which no command line produces and the builder cannot read.
    /// </exception>
    internal static IReadOnlyList<MultipartFormPart> FromMimeBody(EasyMimeBody body) =>
        [.. body.Parts.Select(FromMimePart)];

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
        formEscape ||MailRequestOptionsMapping.SendsFormAsMimeMessage(scheme)
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

    private static MultipartFormPart FromMimePart(EasyMimePart part) =>
        new(
            part.Name,
            KindOf(part),
            ContentOf(part.Data),
            part.Type,
            part.FileName,
            part.Headers,
            part.SubParts is { } subParts ? FromMimeBody(subParts) : [])
        {
            Encoder = part.Encoder,
        };

    private static MultipartFormPartKind KindOf(EasyMimePart part) =>
        part switch
        {
            { SubParts: not null } => MultipartFormPartKind.Multipart,
            { Data: EasyMimeFile or EasyMimeStandardInput, FileName: null } => MultipartFormPartKind.FileContent,
            { Data: EasyMimeFile or EasyMimeStandardInput } => MultipartFormPartKind.FileUpload,
            _ => MultipartFormPartKind.Text,
        };

    private static string ContentOf(EasyMimeData? data) =>
        data switch
        {
            EasyMimeBytes bytes => Encoding.UTF8.GetString(bytes.Bytes.Span),
            EasyMimeFile file => file.Path,
            EasyMimeStandardInput => MultipartFormPart.StandardInputPath,
            null => string.Empty,
            _ => throw new NotSupportedException("A multipart form part read through a callback cannot be built into a command-line form body."),
        };
}
