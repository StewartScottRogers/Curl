using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// The algorithm names this build implements, and what the negotiation needs to know about
/// a name beyond whether both sides offer it (ADR-0122).
/// </summary>
/// <param name="implementedNames">Every name a <c>KEXINIT</c> may offer.</param>
internal sealed class SshAlgorithmCatalogue(IEnumerable<string> implementedNames)
{
    /// <summary>The RFC 8308 signal that the client accepts <c>SSH_MSG_EXT_INFO</c>.</summary>
    internal const string ExtensionInfoClient = "ext-info-c";

    /// <summary>The client's strict key exchange signal (OpenSSH's Terrapin countermeasure).</summary>
    internal const string StrictKeyExchangeClient = "kex-strict-c-v00@openssh.com";

    /// <summary>The server's strict key exchange signal.</summary>
    internal const string StrictKeyExchangeServer = "kex-strict-s-v00@openssh.com";

    /// <summary>The compression method that compresses nothing.</summary>
    internal const string NoCompression = "none";

    private static readonly HashSet<string> AuthenticatedEncryptionCiphers =
    [
        "chacha20-poly1305@openssh.com",
        "aes256-gcm@openssh.com",
        "aes128-gcm@openssh.com",
    ];

    private readonly HashSet<string> implemented = [.. implementedNames];

    /// <summary>
    /// Gets what this build implements so far: the Curve25519, NIST-curve and finite-field key
    /// exchanges, the RSA, ECDSA, DSA and Ed25519 host keys, every cipher and MAC of the presets
    /// (ADR-0122), the two key-exchange signals, <c>zlib</c>, <c>zlib@openssh.com</c> and no
    /// compression.
    /// </summary>
    internal static SshAlgorithmCatalogue Implemented { get; } =
        new([.. SshKeyExchangeMethods.Names, .. SshSignatureVerifiers.Names, .. SshPacketProtections.Names, ExtensionInfoClient, StrictKeyExchangeClient, .. SshCompressionMethods.Names, NoCompression]);

    /// <summary>
    /// Gets a value indicating whether <paramref name="name" /> in a key-exchange list is a
    /// signal (<c>ext-info-c</c>, strict key exchange) rather than a method, and so is never
    /// chosen.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true" /> for a signal.</returns>
    internal static bool IsKeyExchangeSignal(string name) =>
        name is ExtensionInfoClient or StrictKeyExchangeClient;

    /// <summary>
    /// Gets a value indicating whether <paramref name="cipher" /> authenticates its own
    /// packets, so no MAC is negotiated beside it (RFC 5647, OpenSSH's
    /// <c>PROTOCOL.chacha20poly1305</c>).
    /// </summary>
    /// <param name="cipher">The cipher name.</param>
    /// <returns><see langword="true" /> for an AEAD cipher.</returns>
    internal static bool IsAuthenticatedEncryption(string cipher) => AuthenticatedEncryptionCiphers.Contains(cipher);

    /// <summary>
    /// Keeps the names of <paramref name="preferred" /> this build implements, in their order.
    /// </summary>
    /// <param name="preferred">A preset's list.</param>
    /// <returns>The list to offer.</returns>
    internal IReadOnlyList<string> KeepImplemented(IReadOnlyList<string> preferred) =>
        [.. preferred.Where(implemented.Contains)];
}
