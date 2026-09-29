using System.Security.Cryptography;
using System.Text;

namespace Curl.Tls;

/// <summary>
/// The TLS pseudorandom function and the secrets TLS 1.2 and below derive with it:
/// <see cref="Md5Sha1" /> is the PRF of TLS 1.0 and 1.1 (RFC 2246 section 5, P_MD5 over
/// the first half of the secret XOR P_SHA-1 over the second), <see cref="Sha256" /> and
/// <see cref="Sha384" /> are TLS 1.2's P_SHA256 and P_SHA384 (RFC 5246 section 5), the
/// latter for suites whose name ends in <c>_SHA384</c>.
/// </summary>
public sealed class TlsPrf
{
    /// <summary>The length in bytes of a master secret.</summary>
    public const int MasterSecretLength = 48;

    /// <summary>The length in bytes of a Finished message's <c>verify_data</c>.</summary>
    public const int VerifyDataLength = 12;

    private readonly HashAlgorithmName? hashAlgorithm;

    private TlsPrf(HashAlgorithmName? hashAlgorithm) => this.hashAlgorithm = hashAlgorithm;

    /// <summary>Gets the PRF of TLS 1.0 and 1.1: P_MD5 XOR P_SHA-1 over the two halves of the secret.</summary>
    public static TlsPrf Md5Sha1 { get; } = new(null);

    /// <summary>Gets TLS 1.2's P_SHA256, the PRF of every TLS 1.2 suite not named <c>_SHA384</c>.</summary>
    public static TlsPrf Sha256 { get; } = new(HashAlgorithmName.SHA256);

    /// <summary>Gets TLS 1.2's P_SHA384, the PRF of the suites named <c>_SHA384</c>.</summary>
    public static TlsPrf Sha384 { get; } = new(HashAlgorithmName.SHA384);

    /// <summary>Computes <c>PRF(secret, label, seed)</c> to <paramref name="length" /> bytes.</summary>
    /// <param name="secret">The secret.</param>
    /// <param name="label">The ASCII label, such as <c>master secret</c>.</param>
    /// <param name="seed">The seed that follows the label.</param>
    /// <param name="length">How many bytes to produce.</param>
    /// <returns>The PRF output.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> is negative.</exception>
    public byte[] Compute(ReadOnlySpan<byte> secret, string label, ReadOnlySpan<byte> seed, int length)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        byte[] labelAndSeed = [.. Encoding.ASCII.GetBytes(label), .. seed];
        byte[] output = new byte[length];
        if (hashAlgorithm is { } hash)
        {
            ExpandInto(hash, secret, labelAndSeed, output);
            return output;
        }

        int halfLength = (secret.Length + 1) / 2;
        byte[] sha1Output = new byte[length];
        ExpandInto(HashAlgorithmName.MD5, secret[..halfLength], labelAndSeed, output);
        ExpandInto(HashAlgorithmName.SHA1, secret[(secret.Length - halfLength)..], labelAndSeed, sha1Output);
        for (int index = 0; index < length; index++)
        {
            output[index] ^= sha1Output[index];
        }

        return output;
    }

    /// <summary>
    /// Computes the master secret from the pre-master secret and both hello randoms (RFC
    /// 5246 section 8.1): <c>PRF(pre_master_secret, "master secret", ClientHello.random + ServerHello.random)</c>.
    /// </summary>
    /// <param name="preMasterSecret">The pre-master secret the key exchange agreed.</param>
    /// <param name="clientRandom">The ClientHello random.</param>
    /// <param name="serverRandom">The ServerHello random.</param>
    /// <returns>The 48-byte master secret.</returns>
    public byte[] ComputeMasterSecret(ReadOnlySpan<byte> preMasterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom) =>
        Compute(preMasterSecret, "master secret", [.. clientRandom, .. serverRandom], MasterSecretLength);

    /// <summary>
    /// Computes the extended master secret (RFC 7627 section 4):
    /// <c>PRF(pre_master_secret, "extended master secret", session_hash)</c>.
    /// </summary>
    /// <param name="preMasterSecret">The pre-master secret the key exchange agreed.</param>
    /// <param name="sessionHash">The handshake hash up to and including ClientKeyExchange.</param>
    /// <returns>The 48-byte master secret.</returns>
    public byte[] ComputeExtendedMasterSecret(ReadOnlySpan<byte> preMasterSecret, ReadOnlySpan<byte> sessionHash) =>
        Compute(preMasterSecret, "extended master secret", sessionHash, MasterSecretLength);

    /// <summary>
    /// Expands the master secret into the key block (RFC 5246 section 6.3):
    /// <c>PRF(master_secret, "key expansion", server_random + client_random)</c>, which
    /// <see cref="Tls12KeyBlock.Partition" /> divides into keys.
    /// </summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="serverRandom">The ServerHello random.</param>
    /// <param name="clientRandom">The ClientHello random.</param>
    /// <param name="length">The key block length, <see cref="Tls12RecordProtectionParameters.KeyBlockLength" />.</param>
    /// <returns>The key block.</returns>
    public byte[] ComputeKeyBlock(ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> serverRandom, ReadOnlySpan<byte> clientRandom, int length) =>
        Compute(masterSecret, "key expansion", [.. serverRandom, .. clientRandom], length);

    /// <summary>
    /// Computes the client Finished message's <c>verify_data</c> (RFC 5246 section 7.4.9):
    /// <c>PRF(master_secret, "client finished", handshake_hash)</c>.
    /// </summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="handshakeHash">The hash of the handshake messages so far.</param>
    /// <returns>The 12-byte <c>verify_data</c>.</returns>
    public byte[] ComputeClientVerifyData(ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> handshakeHash) =>
        Compute(masterSecret, "client finished", handshakeHash, VerifyDataLength);

    /// <summary>
    /// Computes the server Finished message's <c>verify_data</c> (RFC 5246 section 7.4.9):
    /// <c>PRF(master_secret, "server finished", handshake_hash)</c>.
    /// </summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="handshakeHash">The hash of the handshake messages so far.</param>
    /// <returns>The 12-byte <c>verify_data</c>.</returns>
    public byte[] ComputeServerVerifyData(ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> handshakeHash) =>
        Compute(masterSecret, "server finished", handshakeHash, VerifyDataLength);

    /// <summary>
    /// Hashes the handshake messages as the Finished messages, the extended master secret
    /// and a TLS 1.0 or 1.1 CertificateVerify take them: MD5 then SHA-1 (36 bytes) for
    /// <see cref="Md5Sha1" />, otherwise the PRF's own hash (RFC 5246 section 7.4.9).
    /// </summary>
    internal byte[] HashHandshake(ReadOnlySpan<byte> messages) => hashAlgorithm is { } hash
        ? CryptographicOperations.HashData(hash, messages)
        : [.. CryptographicOperations.HashData(HashAlgorithmName.MD5, messages), .. CryptographicOperations.HashData(HashAlgorithmName.SHA1, messages)];

    /// <summary>
    /// P_hash (RFC 5246 section 5): <c>HMAC(secret, A(i) + seed)</c> for <c>A(1)</c>,
    /// <c>A(2)</c> and on, where <c>A(0)</c> is the seed and <c>A(i)</c> is
    /// <c>HMAC(secret, A(i-1))</c>, until <paramref name="output" /> is full.
    /// </summary>
    private static void ExpandInto(HashAlgorithmName hash, ReadOnlySpan<byte> secret, byte[] seed, Span<byte> output)
    {
        byte[] chain = seed;
        int written = 0;
        while (written < output.Length)
        {
            chain = CryptographicOperations.HmacData(hash, secret, chain);
            byte[] block = CryptographicOperations.HmacData(hash, secret, [.. chain, .. seed]);
            int count = Math.Min(block.Length, output.Length - written);
            block.AsSpan(0, count).CopyTo(output[written..]);
            written += count;
        }
    }
}
