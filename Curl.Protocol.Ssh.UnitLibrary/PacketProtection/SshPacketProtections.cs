using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// The ciphers and MACs this library implements, by the name <c>KEXINIT</c> offers them
/// under (every row of ADR-0122's cipher and MAC tables), and how each direction's protection is built
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
        ["aes256-cbc"] = new(32, 16, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForAes(key, iv), mac!)),
        ["rijndael-cbc@lysator.liu.se"] = new(32, 16, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForAes(key, iv), mac!)),
        ["aes192-cbc"] = new(24, 16, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForAes(key, iv), mac!)),
        ["aes128-cbc"] = new(16, 16, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForAes(key, iv), mac!)),
        ["blowfish-cbc"] = new(16, 8, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForBlowfish(key, iv), mac!)),
        ["arcfour128"] = new(16, 8, (key, _, mac) => new CipherAndMacPacketProtection(new Rc4SshCipher(key, Rc4.Rfc4345DiscardLength), mac!)),
        ["arcfour"] = new(16, 8, (key, _, mac) => new CipherAndMacPacketProtection(new Rc4SshCipher(key, 0), mac!)),
        ["cast128-cbc"] = new(16, 8, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForCast128(key, iv), mac!)),
        ["3des-cbc"] = new(24, 8, (key, iv, mac) => new CipherAndMacPacketProtection(CbcSshCipher.ForTripleDes(key, iv), mac!)),
    };

    private static readonly Dictionary<string, SshMacAlgorithm> Macs = new()
    {
        ["hmac-sha2-256"] = new(key => new BclSshHmac(HashAlgorithmName.SHA256, key), 32, 32, IsEncryptThenMac: false),
        ["hmac-sha2-256-etm@openssh.com"] = new(key => new BclSshHmac(HashAlgorithmName.SHA256, key), 32, 32, IsEncryptThenMac: true),
        ["hmac-sha2-512"] = new(key => new BclSshHmac(HashAlgorithmName.SHA512, key), 64, 64, IsEncryptThenMac: false),
        ["hmac-sha2-512-etm@openssh.com"] = new(key => new BclSshHmac(HashAlgorithmName.SHA512, key), 64, 64, IsEncryptThenMac: true),
        ["hmac-sha1"] = new(key => new BclSshHmac(HashAlgorithmName.SHA1, key), 20, 20, IsEncryptThenMac: false),
        ["hmac-sha1-etm@openssh.com"] = new(key => new BclSshHmac(HashAlgorithmName.SHA1, key), 20, 20, IsEncryptThenMac: true),
        ["hmac-sha1-96"] = new(key => new BclSshHmac(HashAlgorithmName.SHA1, key), 20, 12, IsEncryptThenMac: false),
        ["hmac-md5"] = new(key => new BclSshHmac(HashAlgorithmName.MD5, key), 16, 16, IsEncryptThenMac: false),
        ["hmac-md5-etm@openssh.com"] = new(key => new BclSshHmac(HashAlgorithmName.MD5, key), 16, 16, IsEncryptThenMac: true),
        ["hmac-md5-96"] = new(key => new BclSshHmac(HashAlgorithmName.MD5, key), 16, 12, IsEncryptThenMac: false),
        ["hmac-ripemd160"] = new(key => new Ripemd160SshHmac(key), 20, 20, IsEncryptThenMac: false),
        ["hmac-ripemd160@openssh.com"] = new(key => new Ripemd160SshHmac(key), 20, 20, IsEncryptThenMac: false),
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
        return new SshMac(mac.CreateHmac(keys.DeriveKey(integrityPurpose, mac.KeyLength)), mac.Length, mac.IsEncryptThenMac);
    }

    /// <summary>A cipher's key and IV lengths and how its protection is built.</summary>
    /// <param name="KeyLength">The encryption key's length in bytes.</param>
    /// <param name="IvLength">The IV's length in bytes.</param>
    /// <param name="Create">Builds the protection from the key, the IV and the MAC, which an AEAD cipher ignores.</param>
    private sealed record SshCipherAlgorithm(int KeyLength, int IvLength, Func<byte[], byte[], SshMac?, ISshPacketProtection> Create);

    /// <summary>A MAC's HMAC, key length, MAC length and order.</summary>
    /// <param name="CreateHmac">Keys the HMAC with the derived integrity key.</param>
    /// <param name="KeyLength">The integrity key's length in bytes.</param>
    /// <param name="Length">How many MAC bytes follow each packet: fewer than the HMAC's for the <c>-96</c> MACs.</param>
    /// <param name="IsEncryptThenMac">Whether the MAC covers the encrypted packet.</param>
    private sealed record SshMacAlgorithm(Func<byte[], ISshHmac> CreateHmac, int KeyLength, int Length, bool IsEncryptThenMac);
}
