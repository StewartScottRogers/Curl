using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Agreed algorithms for tests that need only a cipher and a MAC.
/// </summary>
internal static class SshTestAlgorithms
{
    /// <summary>
    /// The algorithms with <paramref name="cipher" /> and <paramref name="mac" /> in both
    /// directions; the key exchange and host key are never read.
    /// </summary>
    /// <param name="cipher">The cipher.</param>
    /// <param name="mac">The MAC, or <see langword="null" /> beside an AEAD cipher.</param>
    /// <returns>The algorithms.</returns>
    internal static SshNegotiatedAlgorithms With(string cipher, string? mac) =>
        new("ecdh-sha2-nistp256", "ecdsa-sha2-nistp256", cipher, cipher, mac, mac, "none", "none", IsStrictKeyExchange: true, DiscardServerGuess: false);
}
