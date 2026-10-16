namespace Curl.Api.Options.Network;

/// <summary>
/// Whether a protocol that can upgrade to TLS (FTP, IMAP, POP3, SMTP, LDAP) must do so:
/// libcurl's <c>CURLUSESSL_*</c> values for <c>CURLOPT_USE_SSL</c>, with the same numbers.
/// </summary>
public enum UseSsl
{
    /// <summary><c>CURLUSESSL_NONE</c>: do not attempt TLS (libcurl's default).</summary>
    None = 0,

    /// <summary><c>CURLUSESSL_TRY</c>: try TLS and go on without it if the server refuses.</summary>
    Try = 1,

    /// <summary><c>CURLUSESSL_CONTROL</c>: require TLS on the control connection.</summary>
    Control = 2,

    /// <summary><c>CURLUSESSL_ALL</c>: require TLS on every connection.</summary>
    All = 3,
}
