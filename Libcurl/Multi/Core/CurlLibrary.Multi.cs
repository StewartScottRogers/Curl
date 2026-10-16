using Libcurl.Multi;

namespace Libcurl.Global;

/// <summary>The multi area's part of <see cref="CurlLibrary"/>: <c>curl_multi_init</c>.</summary>
public sealed partial class CurlLibrary
{
    /// <summary><c>curl_multi_init</c>: a new multi handle over a new engine multi transfer.</summary>
    /// <returns>The new handle; the caller disposes it (<c>curl_multi_cleanup</c>).</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    public CurlMulti MultiInit() => new(CreateMultiTransfer());
}
