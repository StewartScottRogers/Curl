using System.Security.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// The ciphers and MACs this library implements, by the name <c>KEXINIT</c> offers them
/// under (ADR-0122's cipher and MAC tables; the legacy rows join with BL-680), and how each direction's protection is built
/// from the negotiated names and the keys <c>NEWKEYS</c> hands out.
/// </summary>
internal static class SshPacketProtections
{
    private static readonly Dictionary<string, SshCipherAlgorithm> Ciphers = new()
    {
        ["chacha20-poly1305@openssh.com"] = new(ChaCha20Poly1305PacketProtection.KeyLength, 0, (key, _, _) => new ChaCha20Poly1305PacketProtection(key)),
        ["aes256-gcm@openssh.com"] = new(32, AesGcmPacketProtection.NonceLength, (key, iv, _) => new AesGcmPacketProtection(key, iv)),
        ["aes128-gcm@openssh.com"] = new(16, AesGcmPacketProtection.NonceLength, (key, iv, _) => new AesGcmPacketProtection(key, iv)),
        ["aes256-ctr"] = new(32, 16, (key, iv, mac) => new CipherAndMacPacketProtection(new AesCtrSshCipher(key, iv), mac!)),
        ["aes192-ctr"] = new(24, 16, (key, iv, mac) => new CipherAndMacPacketProtection(new AesCtrSshCipher(key, iv), mac!)),
        ["aes128-ctr"] = new(16, 16, (key, iv, mac) => new CipherAndMacPacketProtection(new AesCtrSshCipher(key, iv), mac!)),
    };

    private static readonly Dictionary<string, SshMacAlgorithm> Macs = new()
    {
        ["hmac-sha2-256"] = new(HashAlgorithmName.SHA256, 32, 32, IsEncryptThenMac: false),
        ["hmac-sha2-256-etm@openssh.com"] = new(HashAlgorithmName.SHA256, 32, 32, IsEncryptThenMac: true),
        ["hmac-sha2-512"] = new(HashAlgorithmName.SHA512, 64, 64, IsEncryptThenMac: false),
        ["hmac-sha2-512-etm@openssh.com"] = new(HashAlgorithmName.SHA512, 64, 64, IsEncryptThenMac: true),
    };

    /// <summary>Gets the names of the implemented ciphers and MACs.</summary>
    internal static IEnumerable<string> Names => Ciphers.Keys.Concat(Macs.Keys);

    /// <summary>
    /// Creates the protection for the client's packets: keys <c>A</c>, <c>C</c> and <c>E</c>.
    /// </summary>
    /// <param name="algorithms">The agreed algorithms.</param>
    /// <param name="keys">The key exchange's derivation.</param>
    /// <returns>The protection.</returns>
    /// <exception cref="NotSupportedException">The cipher or MAC is not implemented.</exception>
    internal static ISshPacketProtection ForClientToServer(SshNegotiatedAlgorithms algorithms, SshKeyDerivation keys) =>
        Create(
            algorithms.CipherClientToServer,
            algorithms.MacClientToServer,
            keys,
            SshKeyPurpose.InitialIvClientToServer,
            SshKeyPurpose.EncryptionKeyClientToServer,
            SshKeyPurpose.IntegrityKeyClientToServer);

    /// <summary>
    /// Creates the protection for the server's packets: keys <c>B</c>, <c>D</c> and <c>F</c>.
    /// </summary>
    /// <param name="algorithms">The agreed algorithms.</param>
    /// <param name="keys">The key exchange's derivation.</param>
    /// <returns>The protection.</returns>
    /// <exception cref="NotSupportedException">The cipher or MAC is not implemented.</exception>
    internal static ISshPacketProtection ForServerToClient(SshNegotiatedAlgorithms algorithms, SshKeyDerivation keys) =>
        Create(
            algorithms.CipherServerToClient,
            algorithms.MacServerToClient,
            keys,
            SshKeyPurpose.InitialIvServerToClient,
            SshKeyPurpose.EncryptionKeyServerToClient,
            SshKeyPurpose.IntegrityKeyServerToClient);

    private static ISshPacketProtection Create(
        string cipherName,
        string? macName,
        SshKeyDerivation keys,
        SshKeyPurpose ivPurpose,
        SshKeyPurpose encryptionPurpose,
        SshKeyPurpose integrityPurpose)
    {
        SshCipherAlgorithm cipher = Ciphers.TryGetValue(cipherName, out SshCipherAlgorithm? found)
            ? found
            : throw new NotSupportedException($"The SSH cipher {cipherName} is not implemented.");
        SshMac? mac = macName is null ? null : CreateMac(macName, keys, integrityPurpose);
        return cipher.Create(keys.DeriveKey(encryptionPurpose, cipher.KeyLength), keys.DeriveKey(ivPurpose, cipher.IvLength), mac);
    }

    private static SshMac CreateMac(string macName, SshKeyDerivation keys, SshKeyPurpose integrityPurpose)
    {
        SshMacAlgorithm mac = Macs.TryGetValue(macName, out SshMacAlgorithm? found)
            ? found
            : throw new NotSupportedException($"The SSH MAC {macName} is not implemented.");
        return new SshMac(mac.Hash, keys.DeriveKey(integrityPurpose, mac.KeyLength), mac.Length, mac.IsEncryptThenMac);
    }

    /// <summary>A cipher's key and IV lengths and how its protection is built.</summary>
    /// <param name="KeyLength">The encryption key's length in bytes.</param>
    /// <param name="IvLength">The IV's length in bytes.</param>
    /// <param name="Create">Builds the protection from the key, the IV and the MAC, which an AEAD cipher ignores.</param>
    private sealed record SshCipherAlgorithm(int KeyLength, int IvLength, Func<byte[], byte[], SshMac?, ISshPacketProtection> Create);

    /// <summary>A MAC's hash, key length, MAC length and order.</summary>
    /// <param name="Hash">The HMAC's hash.</param>
    /// <param name="KeyLength">The integrity key's length in bytes.</param>
    /// <param name="Length">How many MAC bytes follow each packet.</param>
    /// <param name="IsEncryptThenMac">Whether the MAC covers the encrypted packet.</param>
    private sealed record SshMacAlgorithm(HashAlgorithmName Hash, int KeyLength, int Length, bool IsEncryptThenMac);
}
