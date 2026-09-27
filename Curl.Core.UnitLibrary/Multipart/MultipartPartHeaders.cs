using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// Writes the header block of one multipart part as libcurl 8.21.0's
/// <c>Curl_mime_prepare_headers</c> does: the <c>Content-Disposition</c>, <c>Content-Type</c> and
/// <c>Content-Transfer-Encoding</c> it chooses, then the part's own headers, then the empty line.
/// </summary>
internal static class MultipartPartHeaders
{
    private const string ContentTypeLabel = "Content-Type";

    private const string ContentDispositionLabel = "Content-Disposition";

    private const string ContentTransferEncodingLabel = "Content-Transfer-Encoding";

    private const string FileContentTypeDefault = "application/octet-stream";

    private const string MultipartContentTypeDefault = "multipart/mixed";

    private static readonly (string Extension, string ContentType)[] ContentTypesByExtension =
    [
        (".gif", "image/gif"),
        (".jpg", "image/jpeg"),
        (".jpeg", "image/jpeg"),
        (".png", "image/png"),
        (".svg", "image/svg+xml"),
        (".txt", "text/plain"),
        (".htm", "text/html"),
        (".html", "text/html"),
        (".pdf", "application/pdf"),
        (".xml", "application/xml"),
    ];

    /// <summary>Formats the header block of <paramref name="part" />.</summary>
    /// <param name="part">The part.</param>
    /// <param name="disposition">
    /// The disposition its enclosing multipart gives it: <c>form-data</c> inside a
    /// <c>multipart/form-data</c>, otherwise <see langword="null" />, which lets the part fall back
    /// to <c>attachment</c>.
    /// </param>
    /// <param name="boundary">The boundary of a multipart part's own parts; <see langword="null" /> for any other part.</param>
    /// <param name="transferEncoding">
    /// The name of the encoder the part's body is sent in, or <see langword="null" /> when it is
    /// sent as it is; the part's own <c>Content-Transfer-Encoding</c> header replaces it.
    /// </param>
    /// <param name="contentType">The content type the part was sent with, or <see langword="null" /> when it was sent without one.</param>
    /// <returns>Every header line, each ending CRLF, followed by the CRLF that ends the block.</returns>
    internal static string Format(MultipartFormPart part, string? disposition, string? boundary, string? transferEncoding, out string? contentType)
    {
        string? fileName = FileNameOf(part);
        contentType = ChooseContentType(part, fileName);
        StringBuilder block = new();
        if (FindHeaderValue(part.Headers, ContentDispositionLabel) is null)
        {
            AppendDisposition(block, part.Name, fileName, disposition);
        }

        AppendContentType(block, contentType, boundary);
        AppendTransferEncoding(block, part.Headers, transferEncoding);

        foreach (string header in part.Headers.Where(header => !IsHeader(header, ContentTypeLabel)))
        {
            block.Append(header).Append("\r\n");
        }

        return block.Append("\r\n").ToString();
    }

    /// <summary>
    /// Tells whether <paramref name="contentType" /> is <paramref name="expected" />, alone or
    /// followed by parameters, compared as curl's <c>content_type_match</c> compares.
    /// </summary>
    /// <param name="contentType">The content type, or <see langword="null" />.</param>
    /// <param name="expected">The media type to look for.</param>
    /// <returns><see langword="true" /> when it matches.</returns>
    internal static bool IsContentType(string? contentType, string expected) =>
        contentType is not null
        && contentType.StartsWith(expected, StringComparison.OrdinalIgnoreCase)
        && (contentType.Length == expected.Length || "\t\r\n ;".Contains(contentType[expected.Length], StringComparison.Ordinal));

    private static string? ChooseContentType(MultipartFormPart part, string? fileName)
    {
        string? customContentType = part.ContentType ?? FindHeaderValue(part.Headers, ContentTypeLabel);
        if (customContentType is not null)
        {
            return customContentType;
        }

        string? contentType = DefaultContentType(part, fileName);
        bool plainTextGoesUnlabelled = part.Kind != MultipartFormPartKind.Multipart && fileName is null;
        return plainTextGoesUnlabelled && IsContentType(contentType, "text/plain") ? null : contentType;
    }

    private static string? FileNameOf(MultipartFormPart part) =>
        part.FileName ?? (part.Kind == MultipartFormPartKind.FileUpload ? BaseName(part.Content) : null);

    private static string BaseName(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];

    private static string? DefaultContentType(MultipartFormPart part, string? fileName) => part.Kind switch
    {
        MultipartFormPartKind.Multipart => MultipartContentTypeDefault,
        MultipartFormPartKind.Text => ContentTypeForName(fileName),
        _ => ContentTypeForName(fileName) ?? FileFallbackContentType(part, fileName),
    };

    /// <summary>
    /// The content type of a file part whose file name's extension gave none: its path's, or
    /// <c>application/octet-stream</c> when it has a file name. Standard input goes to libcurl as a
    /// callback part, not a file part, so, as with text, it gets neither.
    /// </summary>
    private static string? FileFallbackContentType(MultipartFormPart part, string? fileName) =>
        part.ReadsStandardInput
            ? null
            : ContentTypeForName(part.Content) ?? (fileName is null ? null : FileContentTypeDefault);

    private static string? ContentTypeForName(string? name) =>
        name is null
            ? null
            : Array.Find(ContentTypesByExtension, entry => name.EndsWith(entry.Extension, StringComparison.OrdinalIgnoreCase)).ContentType;

    private static void AppendContentType(StringBuilder block, string? contentType, string? boundary)
    {
        if (contentType is not null)
        {
            block.Append($"{ContentTypeLabel}: {contentType}");
            block.Append(boundary is null ? "\r\n" : $"; boundary={boundary}\r\n");
        }
    }

    private static void AppendTransferEncoding(StringBuilder block, IReadOnlyList<string> headers, string? transferEncoding)
    {
        if (transferEncoding is not null && FindHeaderValue(headers, ContentTransferEncodingLabel) is null)
        {
            block.Append($"{ContentTransferEncodingLabel}: {transferEncoding}\r\n");
        }
    }

    private static void AppendDisposition(StringBuilder block, string? name, string? fileName, string? disposition)
    {
        // curl falls back to "attachment" for a part with a name, a file name or a
        // non-multipart content type, then drops an "attachment" with neither name, so
        // only a name or a file name ever earns the fallback.
        disposition ??= name is not null || fileName is not null ? "attachment" : null;
        if (disposition is not null)
        {
            block.Append($"{ContentDispositionLabel}: {disposition}")
                .Append(Parameter("name", name))
                .Append(Parameter("filename", fileName))
                .Append("\r\n");
        }
    }

    private static string Parameter(string label, string? value) =>
        value is null ? string.Empty : $"; {label}=\"{Escape(value)}\"";

    private static string Escape(string text) =>
        text.Replace("\"", "%22", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string? FindHeaderValue(IReadOnlyList<string> headers, string label)
    {
        string? header = headers.FirstOrDefault(candidate => IsHeader(candidate, label));
        return header?[(label.Length + 1)..].TrimStart(' ');
    }

    private static bool IsHeader(string header, string label) =>
        header.Length > label.Length
        && header[label.Length] == ':'
        && header.StartsWith(label, StringComparison.OrdinalIgnoreCase);
}
