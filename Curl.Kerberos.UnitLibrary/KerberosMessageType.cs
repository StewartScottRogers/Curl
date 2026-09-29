namespace Curl.Kerberos;

/// <summary>
/// The Kerberos V5 messages this library encodes and decodes, by their <c>msg-type</c>, which
/// is also the number of the application tag each is wrapped in (RFC 4120 section 5.10).
/// </summary>
public enum KerberosMessageType
{
    /// <summary><c>KRB_AS_REQ</c>, <c>[APPLICATION 10]</c>.</summary>
    AsRequest = 10,

    /// <summary><c>KRB_AS_REP</c>, <c>[APPLICATION 11]</c>.</summary>
    AsReply = 11,

    /// <summary><c>KRB_TGS_REQ</c>, <c>[APPLICATION 12]</c>.</summary>
    TgsRequest = 12,

    /// <summary><c>KRB_TGS_REP</c>, <c>[APPLICATION 13]</c>.</summary>
    TgsReply = 13,

    /// <summary><c>KRB_AP_REQ</c>, <c>[APPLICATION 14]</c>.</summary>
    ApRequest = 14,

    /// <summary><c>KRB_AP_REP</c>, <c>[APPLICATION 15]</c>.</summary>
    ApReply = 15,

    /// <summary><c>KRB_CRED</c>, <c>[APPLICATION 22]</c>.</summary>
    Credential = 22,

    /// <summary><c>KRB_ERROR</c>, <c>[APPLICATION 30]</c>.</summary>
    Error = 30,
}
