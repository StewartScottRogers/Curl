namespace Curl.Protocol.Ftp;

/// <summary>
/// How much of an <c>ftp://</c> transfer must run over TLS: curl's <c>CURLOPT_USE_SSL</c>
/// level, which <c>--ssl</c>, <c>--ftp-ssl-control</c> and <c>--ssl-reqd</c> set
/// (ADR-0102's BL-437 addendum).
/// </summary>
internal enum FtpTlsRequirement
{
    /// <summary>No <c>AUTH</c> is sent.</summary>
    None = 0,

    /// <summary>
    /// <c>--ssl</c>: <c>AUTH</c> and <c>PROT P</c> are tried, and a refusal of either goes on
    /// in plaintext.
    /// </summary>
    Try,

    /// <summary>
    /// <c>--ftp-ssl-control</c>: a refused <c>AUTH</c> is exit 64; <c>PROT C</c> keeps the data
    /// connections in plaintext.
    /// </summary>
    ControlConnection,

    /// <summary><c>--ssl-reqd</c>: a refused <c>AUTH</c> or <c>PROT P</c> is exit 64.</summary>
    AllConnections,
}
