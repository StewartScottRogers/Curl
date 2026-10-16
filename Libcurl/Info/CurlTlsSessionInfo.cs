namespace Libcurl.Info;

/// <summary>
/// libcurl's <c>struct curl_tlssessioninfo</c>, the answer to <see cref="CurlInfo.TlsSslPtr"/> and
/// <see cref="CurlInfo.TlsSession"/>: the build's <c>curl_sslbackend</c> and the backend's native
/// session pointer (ADR-0549, ADR-0566).
/// </summary>
/// <param name="Backend">
/// The <c>curl_sslbackend</c> value: 8 (<c>CURLSSLBACKEND_SCHANNEL</c>) on Windows, the Schannel
/// build's shape, and 1 (<c>CURLSSLBACKEND_OPENSSL</c>) elsewhere.
/// </param>
/// <param name="Internals">
/// The native TLS pointer upstream gives (Schannel's <c>CtxtHandle</c>, OpenSSL's <c>SSL</c>);
/// always <see langword="null"/>, since Curl's TLS is managed and has no native object to hand out.
/// </param>
public sealed record CurlTlsSessionInfo(long Backend, object? Internals);
