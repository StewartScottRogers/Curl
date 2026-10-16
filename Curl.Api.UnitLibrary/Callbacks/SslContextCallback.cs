using Curl.Api.Codes;

namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_SSL_CTX_FUNCTION</c>: adjusts the TLS library's context before a handshake.
/// <c>CURLOPT_SSL_CTX_DATA</c> is the delegate's closure. Carried for completeness: like curl's
/// Schannel build, Curl's engine answers <see cref="CurlCode.NotBuiltIn"/> when it is set.
/// </summary>
/// <param name="sslContext">The TLS library's context (OpenSSL's <c>SSL_CTX *</c>, for one).</param>
/// <returns><see cref="CurlCode.Ok"/> to go on with the handshake, any other code to fail with it.</returns>
public delegate CurlCode SslContextCallback(nint sslContext);
