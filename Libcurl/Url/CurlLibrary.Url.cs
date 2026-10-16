using Libcurl.Url;

namespace Libcurl.Global;

/// <summary>The URL area's part of <see cref="CurlLibrary"/>: <c>curl_url</c>.</summary>
public sealed partial class CurlLibrary
{
    /// <summary><c>curl_url</c>: a new URL handle, every part unset, over a new engine URL handle.</summary>
    /// <returns>The new handle; the caller disposes it (<c>curl_url_cleanup</c>).</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    public CurlUrlHandle UrlInit() => new(CreateUrlHandle());
}
