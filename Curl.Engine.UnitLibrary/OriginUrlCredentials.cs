using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

/// <summary>
/// curl 8.22.0's <c>stripcredentials</c> (<c>src/tool_xattr.c</c>): the URL a <c>--xattr</c> file's
/// origin attribute stores, parsed with a guessed scheme, its user name and password cleared and read
/// back whole through the URL API (<see cref="ManagedCurlUrlHandle" />).
/// </summary>
public static class OriginUrlCredentials
{
    /// <summary>
    /// Removes the user name and password from <paramref name="url" /> and returns it normalised, as
    /// <c>curl_url_get</c> writes it (<c>https://localhost:45</c> comes back as
    /// <c>https://localhost:45/</c>).
    /// </summary>
    /// <param name="url">The URL, credentials and all.</param>
    /// <returns>
    /// The URL without credentials, or <see langword="null" /> where curl's function returns
    /// <c>NULL</c>: a URL the URL API rejects, such as an unsupported scheme or a bad host name.
    /// </returns>
    public static string? Strip(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        using ManagedCurlUrlHandle handle = new();
        if (handle.Set(CurlUrlPart.Url, url, CurlUrlFlags.GuessScheme) != CurlUrlCode.Ok)
        {
            return null;
        }

        // Clearing a part and reading back a URL that parsed cannot fail, so curl's checks of
        // these three calls never fire; a failed get would leave the value null all the same.
        _ = handle.Set(CurlUrlPart.User, null, CurlUrlFlags.None);
        _ = handle.Set(CurlUrlPart.Password, null, CurlUrlFlags.None);
        _ = handle.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? stripped);
        return stripped;
    }
}
