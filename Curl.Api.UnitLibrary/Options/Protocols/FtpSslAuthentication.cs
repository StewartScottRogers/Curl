namespace Curl.Api.Options.Protocols;

/// <summary>
/// Which <c>AUTH</c> command FTPS tries first: libcurl's <c>CURLFTPAUTH_*</c> values for
/// <c>CURLOPT_FTPSSLAUTH</c>, with the same numbers.
/// </summary>
public enum FtpSslAuthentication
{
    /// <summary><c>CURLFTPAUTH_DEFAULT</c>: let libcurl decide (libcurl's default).</summary>
    Default = 0,

    /// <summary><c>CURLFTPAUTH_SSL</c>: try <c>AUTH SSL</c> first, then <c>AUTH TLS</c>.</summary>
    Ssl = 1,

    /// <summary><c>CURLFTPAUTH_TLS</c>: try <c>AUTH TLS</c> first, then <c>AUTH SSL</c>.</summary>
    Tls = 2,
}
