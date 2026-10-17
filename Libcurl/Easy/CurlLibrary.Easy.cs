using Libcurl.Easy;
using Libcurl.Options;

namespace Libcurl.Global;

/// <summary>The easy area's part of <see cref="CurlLibrary"/>: <c>curl_easy_init</c>.</summary>
public sealed partial class CurlLibrary
{
    /// <summary><c>curl_easy_init</c>: a new easy handle over a new engine transfer.</summary>
    /// <returns>The new handle; the caller disposes it (<c>curl_easy_cleanup</c>).</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    public CurlEasy EasyInit() => new(CreateEasyTransfer(), EasyOptionDispatch.Default, CreateEasyAllocations());
}
