using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// UTF-8 that keeps every byte: decoding turns each byte <c>b</c> outside a valid UTF-8 sequence into
/// the lone surrogate U+DC00 + <c>b</c> (the "surrogate escape" scheme), and encoding turns each lone
/// surrogate U+DC80 to U+DCFF back into its byte, so valid text encodes exactly as UTF-8. It is
/// <see cref="SurrogateEscapeUtf8.Encoding" />, the credential encoding off Windows
/// (<see cref="CredentialEncoding.ForPlatform" />), so a URL credential that percent-decodes to bytes
/// that are not UTF-8 (<c>http://user:pa%FFss@host/</c>) is sent as those bytes, as curl 8.18.0
/// OpenSSL on Linux sends them (CGR-053, ADR-0920). It reports UTF-8's code page.
/// </summary>
internal static class InvalidUtf8ByteEscapingEncoding
{
    /// <summary>The one instance.</summary>
    public static Encoding Instance => SurrogateEscapeUtf8.Encoding;
}
