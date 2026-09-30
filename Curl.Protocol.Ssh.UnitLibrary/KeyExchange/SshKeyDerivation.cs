using System.Security.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// Derives the session's keys from one key exchange (RFC 4253 section 7.2):
/// <c>K1 = HASH(K || H || X || session_id)</c> for the purpose's letter X, extended by
/// <c>Kn = HASH(K || H || K1 || ... || Kn-1)</c> until the key is long enough. Each cipher
/// and MAC takes the lengths it needs.
/// </summary>
/// <param name="hashAlgorithm">The key-exchange method's hash.</param>
/// <param name="encodedSharedSecret">K in the key-exchange method's encoding, as H hashed it.</param>
/// <param name="exchangeHash">H of this exchange.</param>
/// <param name="sessionIdentifier">H of the session's first exchange.</param>
internal sealed class SshKeyDerivation(
    HashAlgorithmName hashAlgorithm,
    byte[] encodedSharedSecret,
    byte[] exchangeHash,
    byte[] sessionIdentifier)
{
    /// <summary>
    /// Derives one key.
    /// </summary>
    /// <param name="purpose">Which of the six keys.</param>
    /// <param name="length">Its length in bytes.</param>
    /// <returns>The key.</returns>
    internal byte[] DeriveKey(SshKeyPurpose purpose, int length)
    {
        SshWireWriter prefix = new();
        prefix.WriteBytes(encodedSharedSecret);
        prefix.WriteBytes(exchangeHash);
        byte[] secretAndHash = prefix.ToArray();

        byte[] key = CryptographicOperations.HashData(hashAlgorithm, [.. secretAndHash, (byte)purpose, .. sessionIdentifier]);
        while (key.Length < length)
        {
            key = [.. key, .. CryptographicOperations.HashData(hashAlgorithm, [.. secretAndHash, .. key])];
        }

        return key[..length];
    }
}
