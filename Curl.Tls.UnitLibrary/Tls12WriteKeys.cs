namespace Curl.Tls;

/// <summary>One side's write keys from the key block (RFC 5246 section 6.3).</summary>
/// <param name="MacKey">The MAC key; empty for an AEAD.</param>
/// <param name="Key">The bulk cipher key; empty for the null cipher.</param>
/// <param name="Iv">The IV from the key block; empty where records carry an explicit IV.</param>
public sealed record Tls12WriteKeys(byte[] MacKey, byte[] Key, byte[] Iv)
{
    /// <summary>Gets the keys of the initial state: no MAC, no key, no IV.</summary>
    public static Tls12WriteKeys None { get; } = new([], [], []);
}
