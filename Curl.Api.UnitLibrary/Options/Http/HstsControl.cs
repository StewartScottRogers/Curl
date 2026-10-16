namespace Curl.Api.Options.Http;

/// <summary>
/// The <c>CURLHSTS_*</c> bits of <c>CURLOPT_HSTS_CTRL</c>. Each member's value is libcurl's
/// bit.
/// </summary>
[Flags]
public enum HstsControl
{
    /// <summary>0: HSTS is disabled; libcurl's default.</summary>
    None = 0,

    /// <summary><c>CURLHSTS_ENABLE</c> (1): HSTS is enabled.</summary>
    Enable = 1 << 0,

    /// <summary>
    /// <c>CURLHSTS_READONLYFILE</c> (2): the <c>CURLOPT_HSTS</c> cache file is read but never
    /// written.
    /// </summary>
    ReadOnlyFile = 1 << 1,
}
