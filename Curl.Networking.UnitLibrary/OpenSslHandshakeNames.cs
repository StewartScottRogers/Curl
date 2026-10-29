using System.Collections.Frozen;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Names a hand-built handshake's negotiated group and the server's signature type as curl's
/// OpenSSL build prints them on its <c>SSL connection using</c> line: the group as
/// <c>SSL_get0_group_name</c> names it, the signature type as
/// <c>OBJ_nid2sn(SSL_get_peer_signature_type_nid)</c> does (ADR-0983, BL-4009).
/// </summary>
internal static class OpenSslHandshakeNames
{
    private static readonly FrozenDictionary<ushort, string> GroupNames = new Dictionary<ushort, string>
    {
        [TlsNamedGroup.Secp256r1] = "secp256r1",
        [TlsNamedGroup.Secp384r1] = "secp384r1",
        [TlsNamedGroup.Secp521r1] = "secp521r1",
        [TlsNamedGroup.BrainpoolP256r1] = "brainpoolP256r1",
        [TlsNamedGroup.BrainpoolP384r1] = "brainpoolP384r1",
        [TlsNamedGroup.BrainpoolP512r1] = "brainpoolP512r1",
        [TlsNamedGroup.BrainpoolP256r1Tls13] = "brainpoolP256r1tls13",
        [TlsNamedGroup.BrainpoolP384r1Tls13] = "brainpoolP384r1tls13",
        [TlsNamedGroup.BrainpoolP512r1Tls13] = "brainpoolP512r1tls13",
        [TlsNamedGroup.X25519] = "x25519",
        [TlsNamedGroup.X448] = "x448",
        [TlsNamedGroup.Ffdhe2048] = "ffdhe2048",
        [TlsNamedGroup.Ffdhe3072] = "ffdhe3072",
        [TlsNamedGroup.Ffdhe4096] = "ffdhe4096",
        [TlsNamedGroup.Ffdhe6144] = "ffdhe6144",
        [TlsNamedGroup.Ffdhe8192] = "ffdhe8192",
        [TlsNamedGroup.MlKem512] = "MLKEM512",
        [TlsNamedGroup.MlKem768] = "MLKEM768",
        [TlsNamedGroup.MlKem1024] = "MLKEM1024",
        [TlsNamedGroup.SecP256r1MlKem768] = "SecP256r1MLKEM768",
        [TlsNamedGroup.X25519MlKem768] = "X25519MLKEM768",
        [TlsNamedGroup.SecP384r1MlKem1024] = "SecP384r1MLKEM1024",
    }.ToFrozenDictionary();

    // rsa_pss_rsae_sha256 to sha512 and rsa_pss_pss_sha256 to sha512.
    private static readonly FrozenSet<ushort> RsaPssSchemes = new ushort[] { 0x0804, 0x0805, 0x0806, 0x0809, 0x080a, 0x080b }.ToFrozenSet();

    // OpenSSL's short names for the server key's algorithm, which is the signature type NID
    // of every scheme but RSA-PSS.
    private static readonly FrozenDictionary<string, string> KeyAlgorithmNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1.2.840.113549.1.1.1"] = "rsaEncryption",
        ["1.2.840.10045.2.1"] = "id-ecPublicKey",
        ["1.3.101.112"] = "ED25519",
        ["1.3.101.113"] = "ED448",
    }.ToFrozenDictionary();

    /// <summary>Names a negotiated group.</summary>
    /// <param name="group">The group's code point, or <see langword="null" /> when none was negotiated.</param>
    /// <returns>OpenSSL's name, or <see langword="null" /> for none or a group it does not name.</returns>
    internal static string? GroupName(ushort? group) =>
        group is { } code && GroupNames.TryGetValue(code, out var name) ? name : null;

    /// <summary>Names the type of the signature the server's key made.</summary>
    /// <param name="scheme">The signature scheme, or <see langword="null" /> when the handshake named none.</param>
    /// <param name="keyAlgorithmOid">The server key's algorithm OID, or <see langword="null" /> when no signature verified.</param>
    /// <returns>OpenSSL's short name, or <see langword="null" /> when no signature verified or its key is one OpenSSL's table here does not name.</returns>
    internal static string? SignatureTypeName(ushort? scheme, string? keyAlgorithmOid) =>
        scheme is { } code && RsaPssSchemes.Contains(code) ? "RSASSA-PSS"
            : keyAlgorithmOid is not null && KeyAlgorithmNames.TryGetValue(keyAlgorithmOid, out var name) ? name
            : null;
}
