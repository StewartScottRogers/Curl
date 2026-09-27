namespace Curl.Output;

/// <summary>
/// The TLS library a curl build uses, which decides how <c>-v</c> and the trace dumps
/// word a TLS handshake (ADR-0009, ADR-0085).
/// </summary>
public enum TlsBackend
{
    /// <summary>
    /// Windows' Schannel, as curl's Windows build uses: only the ALPN lines.
    /// </summary>
    Schannel,

    /// <summary>
    /// OpenSSL, as curl's Linux and macOS builds use: the ALPN lines, the version and
    /// cipher, the server certificate's fields, one line per chain level and the verify
    /// result.
    /// </summary>
    OpenSsl,
}
