namespace Curl.Networking;

/// <summary>
/// The text c-ares' <c>ares_strerror</c> gives each <see cref="DnsLookupFailure" />, which curl's
/// c-ares build prints as <c>Could not resolve host: &lt;host&gt; (&lt;text&gt;)</c> (measured on
/// curl 8.22.0 with c-ares 1.34.8, BL-694).
/// </summary>
public static class DnsLookupFailureText
{
    /// <summary>c-ares' texts, indexed by <see cref="DnsLookupFailure" /> value.</summary>
    private static readonly string[] Texts =
    [
        string.Empty,
        "DNS server returned answer with no data",
        "DNS server claims query was misformatted",
        "DNS server returned general failure",
        "Domain name not found",
        "DNS server does not implement requested operation",
        "DNS server refused query",
        "Misformatted domain name",
        "Misformatted DNS reply",
        "Could not contact DNS servers",
        "Timeout while contacting DNS servers",
        "Misformatted string",
    ];

    /// <summary>
    /// Returns c-ares' text for <paramref name="failure" />; empty for <see cref="DnsLookupFailure.None" />
    /// and for a value the enumeration does not define.
    /// </summary>
    /// <param name="failure">The failure to describe.</param>
    /// <returns>The text, e.g. <c>Domain name not found</c>.</returns>
    public static string Describe(DnsLookupFailure failure) =>
        (uint)failure < (uint)Texts.Length ? Texts[(int)failure] : string.Empty;
}
