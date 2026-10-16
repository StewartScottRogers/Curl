using Libcurl.Share;

namespace Libcurl.Global;

/// <summary>The share area's part of <see cref="CurlLibrary"/>: <c>curl_share_init</c>.</summary>
public sealed partial class CurlLibrary
{
    /// <summary><c>curl_share_init</c>: a new share handle, sharing nothing yet, over a new engine share handle.</summary>
    /// <returns>The new handle; the caller frees it with <see cref="CurlShare.Cleanup"/> (<c>curl_share_cleanup</c>).</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    public CurlShare ShareInit() => new(CreateShareHandle());
}
