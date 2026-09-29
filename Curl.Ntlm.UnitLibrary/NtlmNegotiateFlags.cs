namespace Curl.Ntlm;

/// <summary>
/// The <c>NegotiateFlags</c> of MS-NLMP section 2.2.2.5, with the names curl gives them in
/// <c>lib/vauth/ntlm.c</c> where MS-NLMP's differ. The flag field is a little-endian
/// 32-bit value in every NTLM message.
/// </summary>
[Flags]
public enum NtlmNegotiateFlags : uint
{
    /// <summary>No flag set.</summary>
    None = 0,

    /// <summary><c>NTLMSSP_NEGOTIATE_UNICODE</c>: strings are UTF-16LE.</summary>
    NegotiateUnicode = 1u << 0,

    /// <summary><c>NTLM_NEGOTIATE_OEM</c>: strings are in the OEM character set.</summary>
    NegotiateOem = 1u << 1,

    /// <summary><c>NTLMSSP_REQUEST_TARGET</c>: the server is asked for its target name.</summary>
    RequestTarget = 1u << 2,

    /// <summary><c>NTLMSSP_NEGOTIATE_SIGN</c>.</summary>
    NegotiateSign = 1u << 4,

    /// <summary><c>NTLMSSP_NEGOTIATE_SEAL</c>.</summary>
    NegotiateSeal = 1u << 5,

    /// <summary><c>NTLMSSP_NEGOTIATE_DATAGRAM</c>.</summary>
    NegotiateDatagram = 1u << 6,

    /// <summary><c>NTLMSSP_NEGOTIATE_LM_KEY</c>.</summary>
    NegotiateLmKey = 1u << 7,

    /// <summary><c>NTLMSSP_NEGOTIATE_NTLM</c> (curl's <c>NTLMFLAG_NEGOTIATE_NTLM_KEY</c>).</summary>
    NegotiateNtlm = 1u << 9,

    /// <summary><c>NTLMSSP_ANONYMOUS</c>.</summary>
    Anonymous = 1u << 11,

    /// <summary><c>NTLMSSP_NEGOTIATE_OEM_DOMAIN_SUPPLIED</c>.</summary>
    NegotiateOemDomainSupplied = 1u << 12,

    /// <summary><c>NTLMSSP_NEGOTIATE_OEM_WORKSTATION_SUPPLIED</c>.</summary>
    NegotiateOemWorkstationSupplied = 1u << 13,

    /// <summary><c>NTLMSSP_NEGOTIATE_ALWAYS_SIGN</c>.</summary>
    NegotiateAlwaysSign = 1u << 15,

    /// <summary><c>NTLMSSP_TARGET_TYPE_DOMAIN</c>.</summary>
    TargetTypeDomain = 1u << 16,

    /// <summary><c>NTLMSSP_TARGET_TYPE_SERVER</c>.</summary>
    TargetTypeServer = 1u << 17,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY</c> (curl's
    /// <c>NTLMFLAG_NEGOTIATE_NTLM2_KEY</c>); curl answers a challenge carrying it with
    /// NTLMv2 responses.
    /// </summary>
    NegotiateExtendedSessionSecurity = 1u << 19,

    /// <summary><c>NTLMSSP_NEGOTIATE_IDENTIFY</c>.</summary>
    NegotiateIdentify = 1u << 20,

    /// <summary><c>NTLMSSP_REQUEST_NON_NT_SESSION_KEY</c>.</summary>
    RequestNonNtSessionKey = 1u << 22,

    /// <summary><c>NTLMSSP_NEGOTIATE_TARGET_INFO</c>: the challenge carries target information.</summary>
    NegotiateTargetInfo = 1u << 23,

    /// <summary><c>NTLMSSP_NEGOTIATE_VERSION</c>: the message carries a VERSION structure.</summary>
    NegotiateVersion = 1u << 25,

    /// <summary><c>NTLMSSP_NEGOTIATE_128</c>.</summary>
    Negotiate128 = 1u << 29,

    /// <summary><c>NTLMSSP_NEGOTIATE_KEY_EXCH</c>.</summary>
    NegotiateKeyExchange = 1u << 30,

    /// <summary><c>NTLMSSP_NEGOTIATE_56</c>.</summary>
    Negotiate56 = 1u << 31,
}
