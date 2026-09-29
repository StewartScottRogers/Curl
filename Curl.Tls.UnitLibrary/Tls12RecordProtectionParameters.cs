using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// What protects TLS 1.2, 1.1 and 1.0 records once a suite is negotiated: the version,
/// the bulk cipher, the MAC, and whether encrypt-then-MAC (RFC 7366) was agreed. The key
/// lengths it implies size the key block (RFC 5246 section 6.3).
/// </summary>
/// <param name="Version">The negotiated protocol version.</param>
/// <param name="BulkCipher">The suite's bulk cipher.</param>
/// <param name="MacAlgorithm">The suite's record MAC; <see cref="Tls12MacAlgorithm.None" /> for an AEAD.</param>
/// <param name="EncryptThenMac">Whether both sides sent <c>encrypt_then_mac</c>; it changes only CBC records.</param>
public sealed record Tls12RecordProtectionParameters(
    TlsProtocolVersion Version,
    Tls12BulkCipher BulkCipher,
    Tls12MacAlgorithm MacAlgorithm,
    bool EncryptThenMac = false)
{
    // Indexed by Tls12BulkCipher: the record layout, the key length, the CBC block size and the AEAD tag length.
    private static readonly (Tls12CipherMode Mode, int KeyLength, int BlockSize, int TagLength)[] BulkCipherShapes =
    [
        (Tls12CipherMode.Null, 0, 0, 0),
        (Tls12CipherMode.Cbc, 24, 8, 0),
        (Tls12CipherMode.Cbc, 16, 16, 0),
        (Tls12CipherMode.Cbc, 32, 16, 0),
        (Tls12CipherMode.Cbc, 16, 16, 0),
        (Tls12CipherMode.Cbc, 32, 16, 0),
        (Tls12CipherMode.ExplicitNonceAead, 16, 0, 16),
        (Tls12CipherMode.ExplicitNonceAead, 32, 0, 16),
        (Tls12CipherMode.ExplicitNonceAead, 16, 0, 16),
        (Tls12CipherMode.ExplicitNonceAead, 32, 0, 16),
        (Tls12CipherMode.XorNonceAead, 32, 0, 16),
        (Tls12CipherMode.ExplicitNonceAead, 16, 0, 16),
        (Tls12CipherMode.ExplicitNonceAead, 32, 0, 16),
        (Tls12CipherMode.ExplicitNonceAead, 16, 0, 8),
        (Tls12CipherMode.ExplicitNonceAead, 32, 0, 8),
        (Tls12CipherMode.Stream, 16, 0, 0),
    ];

    // Indexed by Tls12MacAlgorithm.
    private static readonly HashAlgorithmName[] MacHashes =
    [
        default,
        HashAlgorithmName.MD5,
        HashAlgorithmName.SHA1,
        HashAlgorithmName.SHA256,
        HashAlgorithmName.SHA384,
    ];

    private static readonly int[] MacLengths = [0, 16, 20, 32, 48];

    /// <summary>Gets the length in bytes of each direction's MAC key.</summary>
    public int MacKeyLength => MacLengths[(int)MacAlgorithm];

    /// <summary>Gets the length in bytes of each direction's bulk cipher key.</summary>
    public int KeyLength => BulkCipherShapes[(int)BulkCipher].KeyLength;

    /// <summary>
    /// Gets the length in bytes of each direction's IV from the key block: the block size
    /// for CBC in TLS 1.0 (chained IVs), none for CBC in TLS 1.1 and 1.2 (explicit IVs,
    /// RFC 5246 section 6.3), 4 for the GCM and CCM salt, 12 for ChaCha20-Poly1305
    /// and none for RC4.
    /// </summary>
    public int FixedIvLength => Mode switch
    {
        Tls12CipherMode.Cbc => Version == TlsProtocolVersion.Tls10 ? BlockSize : 0,
        Tls12CipherMode.ExplicitNonceAead => 4,
        Tls12CipherMode.XorNonceAead => 12,
        _ => 0,
    };

    /// <summary>Gets the key block length: both directions' MAC keys, keys and IVs.</summary>
    public int KeyBlockLength => 2 * (MacKeyLength + KeyLength + FixedIvLength);

    /// <summary>Gets how the bulk cipher lays out a record.</summary>
    internal Tls12CipherMode Mode => BulkCipherShapes[(int)BulkCipher].Mode;

    /// <summary>Gets the CBC block size, or zero for the other modes.</summary>
    internal int BlockSize => BulkCipherShapes[(int)BulkCipher].BlockSize;

    /// <summary>Gets the AEAD tag length: 8 for CCM8, 16 for the other AEADs, zero for the other modes.</summary>
    internal int TagLength => BulkCipherShapes[(int)BulkCipher].TagLength;

    /// <summary>Gets the MAC's hash; meaningless for <see cref="Tls12MacAlgorithm.None" />.</summary>
    internal HashAlgorithmName MacHash => MacHashes[(int)MacAlgorithm];

    /// <summary>
    /// Throws unless the combination is one a suite can name and <paramref name="keys" />
    /// has the lengths it implies.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An AEAD below TLS 1.2 or with a MAC, a CBC cipher or RC4 without a MAC, or a key of the wrong length.
    /// </exception>
    internal void Validate(Tls12WriteKeys keys)
    {
        if (!NamesASuite())
        {
            throw new ArgumentException($"{BulkCipher} with {MacAlgorithm} in {Version} is no suite: an AEAD needs TLS 1.2 and no record MAC, and a CBC cipher or RC4 needs a MAC.", nameof(keys));
        }

        if (!HasKeyLengths(keys))
        {
            throw new ArgumentException($"{BulkCipher} with {MacAlgorithm} in {Version} needs a {MacKeyLength}-byte MAC key, a {KeyLength}-byte key and a {FixedIvLength}-byte IV.", nameof(keys));
        }
    }

    private bool NamesASuite() => Mode switch
    {
        Tls12CipherMode.Null => true,
        Tls12CipherMode.Cbc or Tls12CipherMode.Stream => MacAlgorithm != Tls12MacAlgorithm.None,
        _ => Version == TlsProtocolVersion.Tls12 && MacAlgorithm == Tls12MacAlgorithm.None,
    };

    private bool HasKeyLengths(Tls12WriteKeys keys) =>
        keys.MacKey.Length == MacKeyLength && keys.Key.Length == KeyLength && keys.Iv.Length == FixedIvLength;
}
