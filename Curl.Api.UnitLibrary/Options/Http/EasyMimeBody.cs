namespace Curl.Api.Options.Http;

/// <summary>
/// The data model of a libcurl <c>curl_mime</c> handle: a multipart body built with
/// <c>curl_mime_init</c> and <c>curl_mime_addpart</c> (or <c>curl_formadd</c>), set with
/// <c>CURLOPT_MIMEPOST</c> or <c>CURLOPT_HTTPPOST</c>, or nested in a part with
/// <c>curl_mime_subparts</c>.
/// </summary>
public sealed record EasyMimeBody
{
    /// <summary>The parts in the order <c>curl_mime_addpart</c> added them. Default empty.</summary>
    public IReadOnlyList<EasyMimePart> Parts { get; init; } = [];
}
