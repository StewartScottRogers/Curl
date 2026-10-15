namespace Curl.Api.Options.Http;

/// <summary>
/// The <c>CURLMIMEOPT_*</c> bits of <c>CURLOPT_MIME_OPTIONS</c>. Each member's value is
/// libcurl's bit.
/// </summary>
[Flags]
public enum MimeOptions
{
    /// <summary>0: no option; libcurl's default.</summary>
    None = 0,

    /// <summary>
    /// <c>CURLMIMEOPT_FORMESCAPE</c> (1): form field and file names are backslash-escaped
    /// rather than percent-encoded.
    /// </summary>
    FormEscape = 1 << 0,
}
