namespace Curl.Extension.RateLimit;

/// <summary>
/// One member of a <c>RateLimit</c> or <c>RateLimit-Policy</c> field: the policy it names and its integer parameters
/// (<c>q</c>, <c>w</c>, <c>r</c>, <c>t</c>; draft-ietf-httpapi-ratelimit-headers-11 §3.1, §4.1).
/// </summary>
/// <param name="Name">The quota policy's name, without the quotes of its String.</param>
/// <param name="Parameters">Each parameter whose value is a non-negative Integer, by key.</param>
internal sealed record RateLimitItem(string Name, IReadOnlyDictionary<string, long> Parameters)
{
    /// <summary>Gives a parameter's Integer value.</summary>
    /// <param name="key">The parameter's key.</param>
    /// <returns>Its value, or <see langword="null" /> when the member has no Integer parameter of that key.</returns>
    public long? ParameterOf(string key) => Parameters.TryGetValue(key, out var value) ? value : null;
}
