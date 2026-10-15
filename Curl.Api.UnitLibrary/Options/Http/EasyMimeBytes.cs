namespace Curl.Api.Options.Http;

/// <summary>
/// <c>curl_mime_data</c>: a part's content given as bytes, which libcurl copies.
/// </summary>
/// <param name="Bytes">The content.</param>
public sealed record EasyMimeBytes(ReadOnlyMemory<byte> Bytes) : EasyMimeData;
