namespace Curl.Api.Options.Http;

/// <summary>
/// <c>curl_mime_filedata</c>: a part's content read from a local file when the transfer
/// sends it.
/// </summary>
/// <param name="Path">The local file's path.</param>
public sealed record EasyMimeFile(string Path) : EasyMimeData;
