namespace Curl.Networking;

/// <summary>How a reply matches the query it may answer (<see cref="DnsServerQuery.Match" />).</summary>
public enum DnsReplyMatch
{
    /// <summary>Not a response to this query: another ID or question, or too short; it is ignored.</summary>
    Mismatch,

    /// <summary>The response to this query, truncated (TC set): the query is asked again over TCP.</summary>
    Truncated,

    /// <summary>The whole response to this query.</summary>
    Complete,
}
