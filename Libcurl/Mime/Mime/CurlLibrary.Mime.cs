using Libcurl.Mime;

namespace Libcurl.Global;

/// <summary>The MIME area's part of <see cref="CurlLibrary"/>: <c>curl_mime_init</c>.</summary>
public sealed partial class CurlLibrary
{
    /// <summary><c>curl_mime_init</c>: a new MIME handle with no parts, over a new engine MIME builder.</summary>
    /// <returns>The new handle; the caller frees it (<c>curl_mime_free</c>).</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    public CurlMime MimeInit() => new(CreateMimeBuilder());
}
