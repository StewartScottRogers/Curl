namespace Curl.Api.Options.Http;

/// <summary>
/// <c>curl_mime_filedata</c>: a part's content read from a local file when the transfer
/// sends it.
/// </summary>
/// <param name="Path">The local file's path.</param>
/// <param name="SendsEmptyWhenMissing">
/// Whether a file that cannot be opened is sent as an empty part, as libcurl 8.22.0 sends a
/// <c>curl_mime_filedata</c> part (BL-3211); <see langword="false" />, the tool's <c>-F name=@file</c>, fails the transfer with exit 26 instead.
/// </param>
public sealed record EasyMimeFile(string Path, bool SendsEmptyWhenMissing = false) : EasyMimeData;
