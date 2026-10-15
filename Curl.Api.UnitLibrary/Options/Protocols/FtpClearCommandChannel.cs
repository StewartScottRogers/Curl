namespace Curl.Api.Options.Protocols;

/// <summary>
/// Whether FTPS clears the command channel after authenticating: libcurl's
/// <c>CURLFTPSSL_CCC_*</c> values for <c>CURLOPT_FTP_SSL_CCC</c>, with the same numbers.
/// </summary>
public enum FtpClearCommandChannel
{
    /// <summary><c>CURLFTPSSL_CCC_NONE</c>: do not send <c>CCC</c> (libcurl's default).</summary>
    None = 0,

    /// <summary><c>CURLFTPSSL_CCC_PASSIVE</c>: send <c>CCC</c> and let the server start the TLS shutdown.</summary>
    Passive = 1,

    /// <summary><c>CURLFTPSSL_CCC_ACTIVE</c>: send <c>CCC</c> and start the TLS shutdown.</summary>
    Active = 2,
}
