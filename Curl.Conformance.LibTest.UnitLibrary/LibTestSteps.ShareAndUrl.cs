using System.Diagnostics.CodeAnalysis;
using Libcurl.Global;
using Libcurl.Share;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The programs' <c>curl_share_init</c> and <c>curl_url</c> checks, which 8.22.0's <c>first.h</c> has
/// no macro for: the programs write <c>curl_share_init() failed</c> (or <c>curl_url() failed</c>)
/// with no file or line and return <see cref="LibTestErrorCodes.MajorBad"/>, as <c>lib586.c</c> does.
/// </summary>
public sealed partial class LibTestSteps
{
    /// <summary>
    /// <c>share = curl_share_init()</c> on <paramref name="library"/>. A library already cleaned up
    /// gives no handle: <c>curl_share_init() failed</c> and <see cref="Result"/> becomes
    /// <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="library">The library to create the handle from.</param>
    /// <param name="share">The handle, or <see langword="null"/> when the step failed.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public bool ShareInit(CurlLibrary library, [NotNullWhen(true)] out CurlShare? share)
    {
        ArgumentNullException.ThrowIfNull(library);
        share = library.IsCleanedUp ? null : library.ShareInit();
        return share is not null || HandleMissing("curl_share_init");
    }

    /// <summary>
    /// <c>url = curl_url()</c> on <paramref name="library"/>. A library already cleaned up gives no
    /// handle: <c>curl_url() failed</c> and <see cref="Result"/> becomes <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="library">The library to create the handle from.</param>
    /// <param name="url">The handle, or <see langword="null"/> when the step failed.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public bool UrlInit(CurlLibrary library, [NotNullWhen(true)] out CurlUrlHandle? url)
    {
        ArgumentNullException.ThrowIfNull(library);
        url = library.IsCleanedUp ? null : library.UrlInit();
        return url is not null || HandleMissing("curl_url");
    }

    private bool HandleMissing(string function)
    {
        Result = LibTestErrorCodes.MajorBad;
        error.Write($"{function}() failed\n");
        return false;
    }
}
