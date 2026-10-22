using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Tls;

namespace Libcurl.Options;

/// <summary>
/// The setters of the TLS options libcurl has twice, once for the origin and once with a <c>PROXY_</c>
/// prefix: one instance writes <see cref="EasyTlsOptions.Origin"/>, the other <see cref="EasyTlsOptions.Proxy"/>.
/// Each checks its value as curl 8.22.0's <c>lib/setopt.c</c> does.
/// </summary>
/// <param name="read">Reads the peer's options from the TLS group.</param>
/// <param name="write">Returns the TLS group with the peer's options replaced.</param>
internal sealed class TlsPeerOptionSetters(
    Func<EasyTlsOptions, TlsPeerOptions> read,
    Func<EasyTlsOptions, TlsPeerOptions, EasyTlsOptions> write)
{
    /// <summary>The bits of <c>CURLOPT_SSLVERSION</c> that hold the minimum version; the high half holds the maximum.</summary>
    private const long SslVersionMask = 0xffff;

    /// <summary>The bits of <c>CURLOPT_SSLVERSION</c> that hold the maximum version (<c>CURL_SSLVERSION_MAX_*</c>).</summary>
    private const long SslVersionMaximumMask = 0xffff0000;

    /// <summary>The first minimum-version value past <c>CURL_SSLVERSION_TLSv1_3</c>; it and above are <see cref="CurlCode.BadFunctionArgument"/>.</summary>
    private const long FirstUnknownSslVersion = (long)SslVersion.Tls13 + 1;

    /// <summary>The first maximum-version value past <c>CURL_SSLVERSION_MAX_TLSv1_3</c>, already shifted into the high half.</summary>
    private const long FirstUnknownSslVersionMaximum = ((long)SslVersion.Tls13 + 1) << 16;

    /// <summary>The <c>CURLSSLOPT_*</c> bits libcurl keeps from <c>CURLOPT_SSL_OPTIONS</c>; the rest are dropped.</summary>
    private const long SslOptionsMask = 0xff;

    /// <summary>The two low bits of <c>CURLOPT_SSL_VERIFYHOST</c> that turn host verification on when either is set.</summary>
    private const long VerifyHostBits = 3;

    /// <summary>The setters that write <see cref="EasyTlsOptions.Origin"/>.</summary>
    internal static TlsPeerOptionSetters Origin { get; } = new(t => t.Origin, (t, p) => t with { Origin = p });

    /// <summary>The setters that write <see cref="EasyTlsOptions.Proxy"/>, the <c>PROXY_*</c> twins.</summary>
    internal static TlsPeerOptionSetters Proxy { get; } = new(t => t.Proxy, (t, p) => t with { Proxy = p });

    /// <summary><c>CURLOPT_SSLCERT</c>.</summary>
    internal CurlCode SetCertificate(ref EasyOptions o, object? v) => Peer(ref o, p => p with { Certificate = (string?)v });

    /// <summary><c>CURLOPT_SSLCERT_BLOB</c>: the bytes are copied, as <c>CURL_BLOB_COPY</c> does.</summary>
    internal CurlCode SetCertificateBlob(ref EasyOptions o, object? v) => Peer(ref o, p => p with { CertificateBlob = BlobOf(v) });

    /// <summary><c>CURLOPT_SSLCERTTYPE</c>.</summary>
    internal CurlCode SetCertificateType(ref EasyOptions o, object? v) => Peer(ref o, p => p with { CertificateType = (string?)v });

    /// <summary><c>CURLOPT_SSLKEY</c>.</summary>
    internal CurlCode SetKey(ref EasyOptions o, object? v) => Peer(ref o, p => p with { Key = (string?)v });

    /// <summary><c>CURLOPT_SSLKEY_BLOB</c>: the bytes are copied.</summary>
    internal CurlCode SetKeyBlob(ref EasyOptions o, object? v) => Peer(ref o, p => p with { KeyBlob = BlobOf(v) });

    /// <summary><c>CURLOPT_SSLKEYTYPE</c>.</summary>
    internal CurlCode SetKeyType(ref EasyOptions o, object? v) => Peer(ref o, p => p with { KeyType = (string?)v });

    /// <summary><c>CURLOPT_KEYPASSWD</c> (and its aliases <c>SSLCERTPASSWD</c> and <c>SSLKEYPASSWD</c>).</summary>
    internal CurlCode SetKeyPassword(ref EasyOptions o, object? v) => Peer(ref o, p => p with { KeyPassword = (string?)v });

    /// <summary>
    /// <c>CURLOPT_SSLVERSION</c>: the low 16 bits are the minimum, the next 16 the maximum. SSL 2, SSL 3, a
    /// minimum past TLS 1.3 or a maximum past <c>CURL_SSLVERSION_MAX_TLSv1_3</c> is
    /// <see cref="CurlCode.BadFunctionArgument"/>, as libcurl's <c>setopt_SSLVERSION</c> answers.
    /// </summary>
    internal CurlCode SetSslVersion(ref EasyOptions o, object? v)
    {
        var number = NumberOf(v);
        var version = number & SslVersionMask;
        var maximum = number & SslVersionMaximumMask;
        if (version is (long)SslVersion.Ssl2 or (long)SslVersion.Ssl3 or >= FirstUnknownSslVersion
            || maximum >= FirstUnknownSslVersionMaximum)
        {
            return CurlCode.BadFunctionArgument;
        }

        return Peer(ref o, p => p with { SslVersion = (SslVersion)version, SslVersionMaximum = (SslVersionMaximum)maximum });
    }

    /// <summary><c>CURLOPT_SSL_VERIFYPEER</c>: on for any value but 0.</summary>
    internal CurlCode SetVerifyPeer(ref EasyOptions o, object? v) => Peer(ref o, p => p with { VerifyPeer = NumberOf(v) != 0 });

    /// <summary><c>CURLOPT_SSL_VERIFYHOST</c>: on when either of the two low bits is set, so 1 and 2 are the same, as libcurl's <c>!!(arg &amp; 3)</c>.</summary>
    internal CurlCode SetVerifyHost(ref EasyOptions o, object? v) => Peer(ref o, p => p with { VerifyHost = IsVerifyHostOn(v) });

    /// <summary><c>CURLOPT_CAINFO</c>.</summary>
    internal CurlCode SetCertificateAuthorityFile(ref EasyOptions o, object? v) => Peer(ref o, p => p with { CertificateAuthorityFile = (string?)v });

    /// <summary><c>CURLOPT_CAINFO_BLOB</c>: the bytes are copied.</summary>
    internal CurlCode SetCertificateAuthorityBlob(ref EasyOptions o, object? v) => Peer(ref o, p => p with { CertificateAuthorityBlob = BlobOf(v) });

    /// <summary><c>CURLOPT_CAPATH</c>.</summary>
    internal CurlCode SetCertificateAuthorityPath(ref EasyOptions o, object? v) => Peer(ref o, p => p with { CertificateAuthorityPath = (string?)v });

    /// <summary><c>CURLOPT_CRLFILE</c>.</summary>
    internal CurlCode SetRevocationListFile(ref EasyOptions o, object? v) => Peer(ref o, p => p with { RevocationListFile = (string?)v });

    /// <summary><c>CURLOPT_ISSUERCERT</c>.</summary>
    internal CurlCode SetIssuerCertificate(ref EasyOptions o, object? v) => Peer(ref o, p => p with { IssuerCertificate = (string?)v });

    /// <summary><c>CURLOPT_ISSUERCERT_BLOB</c>: the bytes are copied.</summary>
    internal CurlCode SetIssuerCertificateBlob(ref EasyOptions o, object? v) => Peer(ref o, p => p with { IssuerCertificateBlob = BlobOf(v) });

    /// <summary><c>CURLOPT_PINNEDPUBLICKEY</c>.</summary>
    internal CurlCode SetPinnedPublicKey(ref EasyOptions o, object? v) => Peer(ref o, p => p with { PinnedPublicKey = (string?)v });

    /// <summary><c>CURLOPT_SSL_CIPHER_LIST</c>.</summary>
    internal CurlCode SetCipherList(ref EasyOptions o, object? v) => Peer(ref o, p => p with { CipherList = (string?)v });

    /// <summary><c>CURLOPT_TLS13_CIPHERS</c>.</summary>
    internal CurlCode SetTls13Ciphers(ref EasyOptions o, object? v) => Peer(ref o, p => p with { Tls13Ciphers = (string?)v });

    /// <summary><c>CURLOPT_SSL_OPTIONS</c>: the low 8 bits are kept, as libcurl's <c>(unsigned char)(arg &amp; 0xff)</c>.</summary>
    internal CurlCode SetSslOptions(ref EasyOptions o, object? v) => Peer(ref o, p => p with { SslOptions = (SslOptions)(NumberOf(v) & SslOptionsMask) });

    /// <summary>Whether a <c>VERIFYHOST</c> value turns the check on: either of its two low bits set.</summary>
    /// <param name="value">The option's value.</param>
    /// <returns><see langword="true"/> for 1, 2 or 3 in the low bits.</returns>
    internal static bool IsVerifyHostOn(object? value) => (NumberOf(value) & VerifyHostBits) != 0;

    /// <summary>A copy of a blob option's bytes, or <see langword="null"/> to clear it.</summary>
    /// <param name="value">A <see cref="byte"/> array, a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>, or <see langword="null"/>.</param>
    /// <returns>The copied bytes, or <see langword="null"/>.</returns>
    internal static ReadOnlyMemory<byte>? BlobOf(object? value) => value switch
    {
        byte[] bytes => new ReadOnlyMemory<byte>(bytes.ToArray()),
        ReadOnlyMemory<byte> memory => new ReadOnlyMemory<byte>(memory.ToArray()),
        _ => (ReadOnlyMemory<byte>?)null,
    };

    /// <summary>The option's integer value as a <see cref="long"/>.</summary>
    /// <param name="value">A <see cref="long"/>, an <see cref="int"/> or an <see cref="Enum"/>.</param>
    /// <returns>The number.</returns>
    internal static long NumberOf(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);

    /// <summary>Replaces this peer's TLS options with what <paramref name="change"/> returns for them, leaving the other peer's alone; always <see cref="CurlCode.Ok"/>.</summary>
    private CurlCode Peer(ref EasyOptions options, Func<TlsPeerOptions, TlsPeerOptions> change)
    {
        options = options with { Tls = write(options.Tls, change(read(options.Tls))) };
        return CurlCode.Ok;
    }
}
