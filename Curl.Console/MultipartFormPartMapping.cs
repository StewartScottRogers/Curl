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
    /// <summary>Maps <paramref name="parts" />, and the parts nested inside them, in order.</summary>
    /// <param name="parts">The parsed parts, from <see cref="CommandLineOptions.FormParts" />.</param>
    /// <returns>The builder's parts, one for each of <paramref name="parts" />.</returns>
    internal static IReadOnlyList<MultipartFormPart> FromCommandLine(IReadOnlyList<FormPartSpecification> parts) =>
        [.. parts.Select(FromSpecification)];

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
