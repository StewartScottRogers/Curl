namespace Curl.Networking;

/// <summary>
/// The text curl 8.21.0 prints for a <see cref="DnsMessageFailure" /> in its
/// <c>DoH: &lt;text&gt; type A for &lt;host&gt;</c> trace lines (<c>doh_strerror</c>).
/// </summary>
public static class DnsMessageFailureText
{
    /// <summary>curl's texts, indexed by <see cref="DnsMessageFailure" /> value.</summary>
    private static readonly string[] Texts =
    [
        string.Empty,
        "Bad label",
        "Out of range",
        "Label loop",
        "Too small",
        "RDATA length",
        "Malformat",
        "Bad RCODE",
        "Unexpected TYPE",
        "Unexpected CLASS",
        "No content",
        "Bad ID",
        "Name too long",
    ];

    /// <summary>
    /// Returns curl's text for <paramref name="failure" />; empty for <see cref="DnsMessageFailure.None" />
    /// and for a value the enumeration does not define.
    /// </summary>
    /// <param name="failure">The failure to describe.</param>
    /// <returns>The text, e.g. <c>Too small</c> or <c>Bad RCODE</c>.</returns>
    public static string Describe(DnsMessageFailure failure) =>
        (uint)failure < (uint)Texts.Length ? Texts[(int)failure] : string.Empty;
}
