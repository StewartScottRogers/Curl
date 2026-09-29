using System.Collections.Frozen;

namespace Curl.Protocol.Ldap;

/// <summary>
/// <c>libldap</c>'s <c>ldap_err2string</c> text for each LDAP result code, which the OpenLDAP
/// build of curl puts after <c>LDAP remote: search failed </c> (ADR-0166).
/// </summary>
/// <remarks>
/// Recorded from curl 8.18.0 with OpenLDAP 2.6.10 for a SearchResultDone of every code from 1
/// to 128, and 4096, on 2026-09-28 (BL-587); every code it has no text for gives
/// <c>Unknown error</c>. Code 4, <c>sizeLimitExceeded</c>, ends the search successfully, so
/// curl never prints its text.
/// </remarks>
internal static class LibLdapResultText
{
    private const string UnknownError = "Unknown error";

    private static readonly FrozenDictionary<int, string> Texts = new Dictionary<int, string>
    {
        [1] = "Operations error",
        [2] = "Protocol error",
        [3] = "Time limit exceeded",
        [5] = "Compare False",
        [6] = "Compare True",
        [7] = "Authentication method not supported",
        [8] = "Strong(er) authentication required",
        [9] = "Partial results and referral received",
        [10] = "Referral",
        [11] = "Administrative limit exceeded",
        [12] = "Critical extension is unavailable",
        [13] = "Confidentiality required",
        [14] = "SASL bind in progress",
        [16] = "No such attribute",
        [17] = "Undefined attribute type",
        [18] = "Inappropriate matching",
        [19] = "Constraint violation",
        [20] = "Type or value exists",
        [21] = "Invalid syntax",
        [32] = "No such object",
        [33] = "Alias problem",
        [34] = "Invalid DN syntax",
        [35] = "Entry is a leaf",
        [36] = "Alias dereferencing problem",
        [47] = "Proxy Authorization Failure (X)",
        [48] = "Inappropriate authentication",
        [49] = "Invalid credentials",
        [50] = "Insufficient access",
        [51] = "Server is busy",
        [52] = "Server is unavailable",
        [53] = "Server is unwilling to perform",
        [54] = "Loop detected",
        [64] = "Naming violation",
        [65] = "Object class violation",
        [66] = "Operation not allowed on non-leaf",
        [67] = "Operation not allowed on RDN",
        [68] = "Already exists",
        [69] = "Cannot modify object class",
        [70] = "Results too large",
        [71] = "Operation affects multiple DSAs",
        [76] = "Virtual List View error",
        [80] = "Other (e.g., implementation specific) error",
        [113] = "LCUP Resources Exhausted",
        [114] = "LCUP Security Violation",
        [115] = "LCUP Invalid Data",
        [116] = "LCUP Unsupported Scheme",
        [117] = "LCUP Reload Required",
        [118] = "Cancelled",
        [119] = "No Operation to Cancel",
        [120] = "Too Late to Cancel",
        [121] = "Cannot Cancel",
        [122] = "Assertion Failed",
        [123] = "Proxied Authorization Denied",
        [4096] = "Content Sync Refresh Required",
    }.ToFrozenDictionary();

    /// <summary>Gets <c>libldap</c>'s text for <paramref name="resultCode" />.</summary>
    /// <param name="resultCode">The LDAP result code.</param>
    /// <returns>The text, or <c>Unknown error</c> for a code it has none for.</returns>
    public static string Of(int resultCode) => Texts.GetValueOrDefault(resultCode, UnknownError);
}
