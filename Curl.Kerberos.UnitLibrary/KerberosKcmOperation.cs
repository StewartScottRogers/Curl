namespace Curl.Kerberos;

/// <summary>
/// The KCM protocol operations this library sends, with the opcodes of MIT's
/// <c>src/include/kcm.h</c> (shared with Heimdal's KCM and SSSD's <c>sssd-kcm</c>).
/// </summary>
public enum KerberosKcmOperation
{
    /// <summary><c>KCM_OP_GET_PRINCIPAL</c>: the named cache's default principal.</summary>
    GetPrincipal = 8,

    /// <summary><c>KCM_OP_GET_CRED_UUID_LIST</c>: the 16-byte UUIDs of the named cache's credentials.</summary>
    GetCredentialUuidList = 9,

    /// <summary><c>KCM_OP_GET_CRED_BY_UUID</c>: one credential of the named cache, by its UUID.</summary>
    GetCredentialByUuid = 10,

    /// <summary><c>KCM_OP_GET_DEFAULT_CACHE</c>: the name of the daemon's default cache for the caller.</summary>
    GetDefaultCache = 20,

    /// <summary><c>KCM_OP_GET_KDC_OFFSET</c>: how many seconds the KDC's clock is ahead of the client's.</summary>
    GetKdcOffset = 22,
}
