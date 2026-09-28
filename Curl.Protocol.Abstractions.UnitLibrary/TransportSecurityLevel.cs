namespace Curl.Protocol.Abstractions;

/// <summary>
/// Whether a plaintext scheme upgrades its connection to TLS, per <c>--ssl</c>/<c>--ftp-ssl</c>
/// and <c>--ssl-reqd</c>/<c>--ftp-ssl-reqd</c> (ADR-0102).
/// </summary>
public enum TransportSecurityLevel
{
    /// <summary>
    /// The default: no upgrade is attempted.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>--ssl</c>: try the upgrade, and go on in plaintext when the server refuses it.
    /// </summary>
    Try,

    /// <summary>
    /// <c>--ssl-reqd</c>: the upgrade is required, and a refusal fails the transfer.
    /// </summary>
    Required,
}
