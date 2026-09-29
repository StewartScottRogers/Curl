using System.Collections.Frozen;

namespace Curl.Protocol.Ldap;

/// <summary>
/// WinLDAP's <c>ldap_err2string</c> text for each LDAP result code, which the Windows build
/// of curl puts after <c>LDAP local: bind via ldap_win_bind </c> (ADR-0166).
/// </summary>
/// <remarks>
/// Read from <c>wldap32.dll</c>'s <c>ldap_err2stringW</c> on Windows 11 for every code from
/// 0 to 130 on 2026-09-28; every code it has no text for gives the empty string, as code
/// 100 does in curl's message (BL-586). Codes 81 to 97 are WinLDAP's own client-side codes.
/// </remarks>
internal static class WinLdapResultText
{
    /// <summary>WinLDAP's <c>LDAP_UNAVAILABLE</c>, reported when the server closes before answering the LDAPv2 retry.</summary>
    public const int Unavailable = 52;

    /// <summary>WinLDAP's <c>LDAP_SERVER_DOWN</c>, reported when the server closes while the logon bind's retry reads the rootDSE.</summary>
    public const int ServerDown = 81;

    /// <summary>WinLDAP's <c>LDAP_LOCAL_ERROR</c>, reported when the logon bind's security package produces no token.</summary>
    public const int LocalError = 82;

    /// <summary>WinLDAP's <c>LDAP_TIMEOUT</c>, reported when the first bind gets no BindResponse.</summary>
    public const int Timeout = 85;

    private static readonly FrozenDictionary<int, string> Texts = new Dictionary<int, string>
    {
        [0] = "Success",
        [1] = "Operations Error",
        [2] = "Protocol Error",
        [3] = "Time Limit Exceeded",
        [4] = "Size Limit Exceeded",
        [5] = "Compare False",
        [6] = "Compare True",
        [7] = "Authentication Method Not Supported",
        [8] = "Strong Authentication Required",
        [9] = "Referral (v2)",
        [10] = "Referral",
        [11] = "Administration Limit Exceeded",
        [12] = "Unavailable Critical Extension",
        [13] = "Confidentiality Required",
        [16] = "No Such Attribute",
        [17] = "Undefined Type",
        [18] = "Inappropriate Matching",
        [19] = "Constraint Violation",
        [20] = "Attribute Or Value Exists",
        [21] = "Invalid Syntax",
        [32] = "No Such Object",
        [33] = "Alias Problem",
        [34] = "Invalid DN Syntax",
        [35] = "Is Leaf",
        [36] = "Alias Dereference Problem",
        [48] = "Inappropriate Authentication",
        [49] = "Invalid Credentials",
        [50] = "Insufficient Rights",
        [51] = "Busy",
        [52] = "Unavailable",
        [53] = "Unwilling To Perform",
        [54] = "Loop Detected",
        [60] = "Sort Control Missing",
        [61] = "Index range error",
        [64] = "Naming Violation",
        [65] = "Object Class Violation",
        [66] = "Not allowed on Non-leaf",
        [67] = "Not allowed on RDN",
        [68] = "Already Exists",
        [69] = "No Object Class Mods",
        [70] = "Results Too Large",
        [71] = "Affects Multiple DSAs",
        [76] = "Control Error",
        [80] = "Other",
        [81] = "Server Down",
        [82] = "Local Error",
        [83] = "Encoding Error",
        [84] = "Decoding Error",
        [85] = "Timeout",
        [86] = "Auth Unknown",
        [87] = "Filter Error",
        [88] = "User Cancelled",
        [89] = "Parameter Error",
        [90] = "No Memory",
        [91] = "Can't connect to the LDAP server",
        [92] = "Operation not supported by this version of the LDAP protocol",
        [93] = "Specified control was not found in message",
        [94] = "No result present in message",
        [95] = "More results returned",
        [96] = "Loop while handling referrals",
        [97] = "Referral hop limit exceeded",
    }.ToFrozenDictionary();

    /// <summary>Gets WinLDAP's text for <paramref name="resultCode" />.</summary>
    /// <param name="resultCode">The LDAP result code.</param>
    /// <returns>The text, or the empty string for a code WinLDAP has none for.</returns>
    public static string Of(int resultCode) => Texts.GetValueOrDefault(resultCode, string.Empty);
}
