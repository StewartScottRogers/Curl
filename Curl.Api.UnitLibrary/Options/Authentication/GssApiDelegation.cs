namespace Curl.Api.Options.Authentication;

/// <summary>
/// Whether the GSS-API server may act on the user's behalf: the
/// <c>CURLGSSAPI_DELEGATION_*</c> values of <c>CURLOPT_GSSAPI_DELEGATION</c>. Each member's
/// value is libcurl's.
/// </summary>
public enum GssApiDelegation
{
    /// <summary><c>CURLGSSAPI_DELEGATION_NONE</c> (0): no delegation; libcurl's default.</summary>
    None = 0,

    /// <summary>
    /// <c>CURLGSSAPI_DELEGATION_POLICY_FLAG</c> (1): delegate only when the Kerberos policy
    /// marks the service <c>OK-AS-DELEGATE</c>.
    /// </summary>
    PolicyFlag = 1,

    /// <summary><c>CURLGSSAPI_DELEGATION_FLAG</c> (2): always delegate the credentials.</summary>
    Flag = 2,
}
