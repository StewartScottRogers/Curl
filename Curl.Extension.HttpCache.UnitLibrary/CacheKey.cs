using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.HttpCache;

/// <summary>
/// The cache key of a request (RFC 9111 §2): its method and target URI, the URI without user
/// information or fragment, hashed with SHA-256 into 64 lowercase hexadecimal digits that are safe
/// as a file name on every platform.
/// </summary>
public static class CacheKey
{
    /// <summary>The key of a <paramref name="method"/> request for <paramref name="targetUri"/>.</summary>
    public static string For(string method, Uri targetUri)
    {
        var target = targetUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(method + " " + target)));
    }
}
