using Curl.Cli;
using Curl.Core.Multipart;

namespace Curl.Console;

/// <summary>
/// Maps the <c>-F</c> / <c>--form</c> and <c>--form-string</c> parts of a parsed command line,
/// <see cref="FormPartSpecification" />, onto the <see cref="MultipartFormPart" />
/// <see cref="MultipartFormBodyBuilder" /> takes. The two models are separate because
/// <c>Curl.Cli</c> references <c>Curl.Core</c> (ADR-0027).
/// </summary>
/// <remarks>
/// Every field is copied as parsed, <see cref="FormPartSpecification.Encoder" /> included: the
/// builder applies <c>;encoder=</c> and rejects an unknown name (ADR-0041).
/// </remarks>
internal static class MultipartFormPartMapping
{
    private const string ContentTypeName = "Content-Type";

    /// <summary>Maps <paramref name="parts" />, and the parts nested inside them, in order.</summary>
    /// <param name="parts">The parsed parts, from <see cref="CommandLineOptions.FormParts" />.</param>
    /// <returns>The builder's parts, one for each of <paramref name="parts" />.</returns>
    internal static IReadOnlyList<MultipartFormPart> FromCommandLine(IReadOnlyList<FormPartSpecification> parts) =>
        [.. parts.Select(FromSpecification)];

    /// <summary>
    /// The escaping the builder gives names and file names: <see cref="MultipartNameEscaping.Backslash" />
    /// under <c>--form-escape</c>, otherwise curl's default <see cref="MultipartNameEscaping.Percent" /> (BL-625).
    /// </summary>
    /// <param name="options">The option group whose form is being built.</param>
    /// <returns>The escaping <see cref="CommandLineOptions.FormEscape" /> asks for.</returns>
    internal static MultipartNameEscaping NameEscapingOf(CommandLineOptions options) =>
        options.FormEscape ? MultipartNameEscaping.Backslash : MultipartNameEscaping.Percent;

    /// <summary>
    /// The request <c>Content-Type</c> the builder chooses the parts' disposition by, read as
    /// libcurl 8.21.0's <c>Curl_checkheaders</c> reads it: the first <c>-H</c> value naming
    /// <c>Content-Type</c> before a <c>:</c> or <c>;</c>, after that separator and its leading
    /// spaces, so <c>-H "Content-Type:"</c> gives an empty one (BL-1972).
    /// </summary>
    /// <param name="options">The option group whose form is being built.</param>
    /// <returns>The value, or <see langword="null" /> when no <c>-H</c> value names <c>Content-Type</c>.</returns>
    internal static string? RequestContentTypeOf(CommandLineOptions options) =>
        options.Headers
            .Where(header => header.Length > ContentTypeName.Length
                && header[ContentTypeName.Length] is ':' or ';'
                && header.StartsWith(ContentTypeName, StringComparison.OrdinalIgnoreCase))
            .Select(header => header[(ContentTypeName.Length + 1)..].TrimStart(' '))
            .FirstOrDefault();

    private static MultipartFormPart FromSpecification(FormPartSpecification part) =>
        new(
            part.Name,
            KindOf(part.Kind),
            part.Content,
            part.ContentType,
            part.FileName,
            part.Headers,
            FromCommandLine(part.Parts))
        {
            Encoder = part.Encoder,
        };

    private static MultipartFormPartKind KindOf(FormPartKind kind) =>
        kind switch
        {
            FormPartKind.FileUpload => MultipartFormPartKind.FileUpload,
            FormPartKind.FileContent => MultipartFormPartKind.FileContent,
            FormPartKind.Multipart => MultipartFormPartKind.Multipart,
            _ => MultipartFormPartKind.Text,
        };
}
