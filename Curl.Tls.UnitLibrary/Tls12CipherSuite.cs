using static Curl.Tls.Tls12BulkCipher;
using static Curl.Tls.Tls12MacAlgorithm;
using Auth = Curl.Tls.Tls12Authentication;
using Kx = Curl.Tls.Tls12KeyExchange;

namespace Curl.Tls;

/// <summary>
/// A TLS 1.2, 1.1 and 1.0 cipher suite the client can run (ADR-0140, "What the client
/// supports"): its key exchange, how it authenticates the server, its bulk cipher and
/// record MAC, and whether its TLS 1.2 PRF is SHA-384. The table holds every ECDHE, DHE
/// (RSA and DSS), RSA and anonymous suite with one of these bulk ciphers: AES-CBC, AES-GCM,
/// AES-CCM and AES-CCM8 (RFC 6655, RFC 7251), ChaCha20-Poly1305, Camellia-CBC, ARIA-GCM,
/// 3DES, RC4 and NULL.
/// </summary>
/// <param name="Code">The suite's code point.</param>
/// <param name="KeyExchange">How the pre-master secret is agreed.</param>
/// <param name="Authentication">How the server is authenticated.</param>
/// <param name="BulkCipher">The bulk cipher.</param>
/// <param name="MacAlgorithm">The record MAC; <see cref="Tls12MacAlgorithm.None" /> for an AEAD.</param>
/// <param name="UsesSha384Prf">Whether the suite's TLS 1.2 PRF is P_SHA384 (its name ends in <c>_SHA384</c>).</param>
public sealed record Tls12CipherSuite(
    ushort Code,
    Tls12KeyExchange KeyExchange,
    Tls12Authentication Authentication,
    Tls12BulkCipher BulkCipher,
    Tls12MacAlgorithm MacAlgorithm,
    bool UsesSha384Prf)
{
    /// <summary><c>TLS_EMPTY_RENEGOTIATION_INFO_SCSV</c> (RFC 5746 section 3.3): a signal in the suite list, never chosen.</summary>
    public const ushort EmptyRenegotiationInfoScsv = 0x00ff;

    private static readonly Dictionary<ushort, Tls12CipherSuite> Suites = new Tls12CipherSuite[]
    {
        new(0xc02b, Kx.Ecdhe, Auth.Ecdsa, Aes128Gcm, None, false),
        new(0xc02c, Kx.Ecdhe, Auth.Ecdsa, Aes256Gcm, None, true),
        new(0xcca9, Kx.Ecdhe, Auth.Ecdsa, ChaCha20Poly1305, None, false),
        new(0xc023, Kx.Ecdhe, Auth.Ecdsa, Aes128Cbc, HmacSha256, false),
        new(0xc024, Kx.Ecdhe, Auth.Ecdsa, Aes256Cbc, HmacSha384, true),
        new(0xc009, Kx.Ecdhe, Auth.Ecdsa, Aes128Cbc, HmacSha1, false),
        new(0xc00a, Kx.Ecdhe, Auth.Ecdsa, Aes256Cbc, HmacSha1, false),
        new(0xc072, Kx.Ecdhe, Auth.Ecdsa, Camellia128Cbc, HmacSha256, false),
        new(0xc073, Kx.Ecdhe, Auth.Ecdsa, Camellia256Cbc, HmacSha384, true),
        new(0xc05c, Kx.Ecdhe, Auth.Ecdsa, Aria128Gcm, None, false),
        new(0xc05d, Kx.Ecdhe, Auth.Ecdsa, Aria256Gcm, None, true),
        new(0xc008, Kx.Ecdhe, Auth.Ecdsa, TripleDesEdeCbc, HmacSha1, false),
        new(0xc0ac, Kx.Ecdhe, Auth.Ecdsa, Aes128Ccm, None, false),
        new(0xc0ad, Kx.Ecdhe, Auth.Ecdsa, Aes256Ccm, None, false),
        new(0xc0ae, Kx.Ecdhe, Auth.Ecdsa, Aes128Ccm8, None, false),
        new(0xc0af, Kx.Ecdhe, Auth.Ecdsa, Aes256Ccm8, None, false),
        new(0xc007, Kx.Ecdhe, Auth.Ecdsa, Rc4128, HmacSha1, false),
        new(0xc006, Kx.Ecdhe, Auth.Ecdsa, Null, HmacSha1, false),
        new(0xc02f, Kx.Ecdhe, Auth.Rsa, Aes128Gcm, None, false),
        new(0xc030, Kx.Ecdhe, Auth.Rsa, Aes256Gcm, None, true),
        new(0xcca8, Kx.Ecdhe, Auth.Rsa, ChaCha20Poly1305, None, false),
        new(0xc027, Kx.Ecdhe, Auth.Rsa, Aes128Cbc, HmacSha256, false),
        new(0xc028, Kx.Ecdhe, Auth.Rsa, Aes256Cbc, HmacSha384, true),
        new(0xc013, Kx.Ecdhe, Auth.Rsa, Aes128Cbc, HmacSha1, false),
        new(0xc014, Kx.Ecdhe, Auth.Rsa, Aes256Cbc, HmacSha1, false),
        new(0xc076, Kx.Ecdhe, Auth.Rsa, Camellia128Cbc, HmacSha256, false),
        new(0xc077, Kx.Ecdhe, Auth.Rsa, Camellia256Cbc, HmacSha384, true),
        new(0xc060, Kx.Ecdhe, Auth.Rsa, Aria128Gcm, None, false),
        new(0xc061, Kx.Ecdhe, Auth.Rsa, Aria256Gcm, None, true),
        new(0xc012, Kx.Ecdhe, Auth.Rsa, TripleDesEdeCbc, HmacSha1, false),
        new(0xc011, Kx.Ecdhe, Auth.Rsa, Rc4128, HmacSha1, false),
        new(0xc010, Kx.Ecdhe, Auth.Rsa, Null, HmacSha1, false),
        new(0x009e, Kx.Dhe, Auth.Rsa, Aes128Gcm, None, false),
        new(0x009f, Kx.Dhe, Auth.Rsa, Aes256Gcm, None, true),
        new(0xccaa, Kx.Dhe, Auth.Rsa, ChaCha20Poly1305, None, false),
        new(0x0067, Kx.Dhe, Auth.Rsa, Aes128Cbc, HmacSha256, false),
        new(0x006b, Kx.Dhe, Auth.Rsa, Aes256Cbc, HmacSha256, false),
        new(0x0033, Kx.Dhe, Auth.Rsa, Aes128Cbc, HmacSha1, false),
        new(0x0039, Kx.Dhe, Auth.Rsa, Aes256Cbc, HmacSha1, false),
        new(0x00be, Kx.Dhe, Auth.Rsa, Camellia128Cbc, HmacSha256, false),
        new(0x00c4, Kx.Dhe, Auth.Rsa, Camellia256Cbc, HmacSha256, false),
        new(0x0045, Kx.Dhe, Auth.Rsa, Camellia128Cbc, HmacSha1, false),
        new(0x0088, Kx.Dhe, Auth.Rsa, Camellia256Cbc, HmacSha1, false),
        new(0xc052, Kx.Dhe, Auth.Rsa, Aria128Gcm, None, false),
        new(0xc053, Kx.Dhe, Auth.Rsa, Aria256Gcm, None, true),
        new(0x0016, Kx.Dhe, Auth.Rsa, TripleDesEdeCbc, HmacSha1, false),
        new(0xc09e, Kx.Dhe, Auth.Rsa, Aes128Ccm, None, false),
        new(0xc09f, Kx.Dhe, Auth.Rsa, Aes256Ccm, None, false),
        new(0xc0a2, Kx.Dhe, Auth.Rsa, Aes128Ccm8, None, false),
        new(0xc0a3, Kx.Dhe, Auth.Rsa, Aes256Ccm8, None, false),
        new(0x00a2, Kx.Dhe, Auth.Dss, Aes128Gcm, None, false),
        new(0x00a3, Kx.Dhe, Auth.Dss, Aes256Gcm, None, true),
        new(0x0040, Kx.Dhe, Auth.Dss, Aes128Cbc, HmacSha256, false),
        new(0x006a, Kx.Dhe, Auth.Dss, Aes256Cbc, HmacSha256, false),
        new(0x0032, Kx.Dhe, Auth.Dss, Aes128Cbc, HmacSha1, false),
        new(0x0038, Kx.Dhe, Auth.Dss, Aes256Cbc, HmacSha1, false),
        new(0x00bd, Kx.Dhe, Auth.Dss, Camellia128Cbc, HmacSha256, false),
        new(0x00c3, Kx.Dhe, Auth.Dss, Camellia256Cbc, HmacSha256, false),
        new(0x0044, Kx.Dhe, Auth.Dss, Camellia128Cbc, HmacSha1, false),
        new(0x0087, Kx.Dhe, Auth.Dss, Camellia256Cbc, HmacSha1, false),
        new(0xc056, Kx.Dhe, Auth.Dss, Aria128Gcm, None, false),
        new(0xc057, Kx.Dhe, Auth.Dss, Aria256Gcm, None, true),
        new(0x0013, Kx.Dhe, Auth.Dss, TripleDesEdeCbc, HmacSha1, false),
        new(0x009c, Kx.Rsa, Auth.Rsa, Aes128Gcm, None, false),
        new(0x009d, Kx.Rsa, Auth.Rsa, Aes256Gcm, None, true),
        new(0x003c, Kx.Rsa, Auth.Rsa, Aes128Cbc, HmacSha256, false),
        new(0x003d, Kx.Rsa, Auth.Rsa, Aes256Cbc, HmacSha256, false),
        new(0x002f, Kx.Rsa, Auth.Rsa, Aes128Cbc, HmacSha1, false),
        new(0x0035, Kx.Rsa, Auth.Rsa, Aes256Cbc, HmacSha1, false),
        new(0x00ba, Kx.Rsa, Auth.Rsa, Camellia128Cbc, HmacSha256, false),
        new(0x00c0, Kx.Rsa, Auth.Rsa, Camellia256Cbc, HmacSha256, false),
        new(0x0041, Kx.Rsa, Auth.Rsa, Camellia128Cbc, HmacSha1, false),
        new(0x0084, Kx.Rsa, Auth.Rsa, Camellia256Cbc, HmacSha1, false),
        new(0xc050, Kx.Rsa, Auth.Rsa, Aria128Gcm, None, false),
        new(0xc051, Kx.Rsa, Auth.Rsa, Aria256Gcm, None, true),
        new(0x000a, Kx.Rsa, Auth.Rsa, TripleDesEdeCbc, HmacSha1, false),
        new(0x003b, Kx.Rsa, Auth.Rsa, Null, HmacSha256, false),
        new(0x0002, Kx.Rsa, Auth.Rsa, Null, HmacSha1, false),
        new(0x0001, Kx.Rsa, Auth.Rsa, Null, HmacMd5, false),
        new(0xc09c, Kx.Rsa, Auth.Rsa, Aes128Ccm, None, false),
        new(0xc09d, Kx.Rsa, Auth.Rsa, Aes256Ccm, None, false),
        new(0xc0a0, Kx.Rsa, Auth.Rsa, Aes128Ccm8, None, false),
        new(0xc0a1, Kx.Rsa, Auth.Rsa, Aes256Ccm8, None, false),
        new(0x0005, Kx.Rsa, Auth.Rsa, Rc4128, HmacSha1, false),
        new(0x0004, Kx.Rsa, Auth.Rsa, Rc4128, HmacMd5, false),
        new(0x00a6, Kx.Dhe, Auth.Anonymous, Aes128Gcm, None, false),
        new(0x00a7, Kx.Dhe, Auth.Anonymous, Aes256Gcm, None, true),
        new(0x006c, Kx.Dhe, Auth.Anonymous, Aes128Cbc, HmacSha256, false),
        new(0x006d, Kx.Dhe, Auth.Anonymous, Aes256Cbc, HmacSha256, false),
        new(0x0034, Kx.Dhe, Auth.Anonymous, Aes128Cbc, HmacSha1, false),
        new(0x003a, Kx.Dhe, Auth.Anonymous, Aes256Cbc, HmacSha1, false),
        new(0x00bf, Kx.Dhe, Auth.Anonymous, Camellia128Cbc, HmacSha256, false),
        new(0x00c5, Kx.Dhe, Auth.Anonymous, Camellia256Cbc, HmacSha256, false),
        new(0x0046, Kx.Dhe, Auth.Anonymous, Camellia128Cbc, HmacSha1, false),
        new(0x0089, Kx.Dhe, Auth.Anonymous, Camellia256Cbc, HmacSha1, false),
        new(0xc05a, Kx.Dhe, Auth.Anonymous, Aria128Gcm, None, false),
        new(0xc05b, Kx.Dhe, Auth.Anonymous, Aria256Gcm, None, true),
        new(0x001b, Kx.Dhe, Auth.Anonymous, TripleDesEdeCbc, HmacSha1, false),
        new(0x0018, Kx.Dhe, Auth.Anonymous, Rc4128, HmacMd5, false),
        new(0xc018, Kx.Ecdhe, Auth.Anonymous, Aes128Cbc, HmacSha1, false),
        new(0xc019, Kx.Ecdhe, Auth.Anonymous, Aes256Cbc, HmacSha1, false),
        new(0xc017, Kx.Ecdhe, Auth.Anonymous, TripleDesEdeCbc, HmacSha1, false),
        new(0xc016, Kx.Ecdhe, Auth.Anonymous, Rc4128, HmacSha1, false),
        new(0xc015, Kx.Ecdhe, Auth.Anonymous, Null, HmacSha1, false),
    }.ToDictionary(suite => suite.Code);

    /// <summary>Gets every suite the client can run.</summary>
    public static IReadOnlyCollection<Tls12CipherSuite> All => Suites.Values;

    /// <summary>
    /// Gets a value indicating whether the suite exists only in TLS 1.2: an AEAD, or a
    /// SHA-256 or SHA-384 record MAC (RFC 5246 appendix A.5).
    /// </summary>
    public bool RequiresTls12 => MacAlgorithm is None or HmacSha256 or HmacSha384;

    /// <summary>Returns the suite with code point <paramref name="code" />.</summary>
    /// <param name="code">The code point.</param>
    /// <returns>The suite, or <see langword="null" /> when the client cannot run it.</returns>
    public static Tls12CipherSuite? Find(ushort code) => Suites.GetValueOrDefault(code);

    /// <summary>Returns the PRF of this suite in <paramref name="version" />.</summary>
    /// <param name="version">The negotiated version.</param>
    /// <returns>MD5 and SHA-1 below TLS 1.2, otherwise P_SHA256 or P_SHA384.</returns>
    public TlsPrf PrfFor(TlsProtocolVersion version) => version switch
    {
        TlsProtocolVersion.Tls12 => UsesSha384Prf ? TlsPrf.Sha384 : TlsPrf.Sha256,
        _ => TlsPrf.Md5Sha1,
    };

    /// <summary>Returns the record protection this suite gives in <paramref name="version" />.</summary>
    /// <param name="version">The negotiated version.</param>
    /// <param name="encryptThenMac">Whether both sides agreed encrypt-then-MAC; it counts only for a CBC cipher (RFC 7366 section 3).</param>
    /// <returns>The record protection parameters.</returns>
    public Tls12RecordProtectionParameters RecordProtectionFor(TlsProtocolVersion version, bool encryptThenMac)
    {
        Tls12RecordProtectionParameters parameters = new(version, BulkCipher, MacAlgorithm);
        return encryptThenMac && parameters.Mode == Tls12CipherMode.Cbc ? parameters with { EncryptThenMac = true } : parameters;
    }
}
