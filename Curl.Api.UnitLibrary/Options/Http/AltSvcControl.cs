namespace Curl.Api.Options.Http;

/// <summary>
/// The <c>CURLALTSVC_*</c> bits of <c>CURLOPT_ALTSVC_CTRL</c>: how <c>Alt-Svc</c> is
/// handled. Each member's value is libcurl's bit.
/// </summary>
[Flags]
public enum AltSvcControl
{
    /// <summary>0: <c>Alt-Svc</c> handling is disabled; libcurl's default.</summary>
    None = 0,

    /// <summary>
    /// <c>CURLALTSVC_READONLYFILE</c> (4): the <c>CURLOPT_ALTSVC</c> cache file is read but
    /// never written.
    /// </summary>
    ReadOnlyFile = 1 << 2,

    /// <summary><c>CURLALTSVC_H1</c> (8): HTTP/1.1 alternatives are accepted.</summary>
    H1 = 1 << 3,

    /// <summary><c>CURLALTSVC_H2</c> (16): HTTP/2 alternatives are accepted.</summary>
    H2 = 1 << 4,

    /// <summary><c>CURLALTSVC_H3</c> (32): HTTP/3 alternatives are accepted.</summary>
    H3 = 1 << 5,
}
