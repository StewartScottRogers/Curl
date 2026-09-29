namespace Curl.Cryptography;

/// <summary>
/// RFC 9180's base-mode test vectors for the suites <see cref="Hpke" /> supports (Appendix
/// A.1.1, A.2.1, A.3.1 and A.5.1), plus the two AES-256-GCM suites the RFC's appendix
/// abridges, taken from the CFRG's <c>test-vectors.json</c>
/// (github.com/cfrg/draft-irtf-cfrg-hpke), whose other entries the appendix reproduces.
/// </summary>
internal static class HpkeBaseModeVectors
{
    /// <summary>RFC 9180 Appendix A: every base-mode vector's info.</summary>
    internal const string Info = "4f6465206f6e2061204772656369616e2055726e";

    /// <summary>RFC 9180 Appendix A: every encryption's plaintext.</summary>
    internal const string Plaintext = "4265617574792069732074727574682c20747275746820626561757479";

    /// <summary>RFC 9180 Appendix A: the associated data of sequence number 0, "Count-0".</summary>
    internal const string AssociatedData0 = "436f756e742d30";

    /// <summary>RFC 9180 Appendix A: the associated data of sequence number 1, "Count-1".</summary>
    internal const string AssociatedData1 = "436f756e742d31";

    /// <summary>RFC 9180 Appendix A.1.1, DHKEM(X25519, HKDF-SHA256), HKDF-SHA256, AES-128-GCM.</summary>
    internal static readonly HpkeBaseModeVector X25519Aes128Gcm = new(
        "RFC 9180 A.1.1",
        HpkeKem.DhkemX25519HkdfSha256,
        HpkeAead.Aes128Gcm,
        "52c4a758a802cd8b936eceea314432798d5baf2d7e9235dc084ab1b9cfa2f736",
        "3948cfe0ad1ddb695d780e59077195da6c56506b027329794ab02bca80815c4d",
        "4612c550263fc8ad58375df3f557aac531d26850903e55a9f23f21d8534e8ac8",
        "37fda3567bdbd628e88668c3c8d7e97d1d1253b6d4ea6d44c150f741f1bf4431",
        "fe0e18c9f024ce43799ae393c7e8fe8fce9d218875e8227b0187c04e7d2ea1fc",
        "4531685d41d65f03dc48f6b8302c05b0",
        "56d890e5accaaf011cff4b7d",
        "45ff1c2e220db587171952c0592d5f5ebe103f1561a2614e38f2ffd47e99e3f8",
        "f938558b5d72f1a23810b4be2ab4f84331acc02fc97babc53a52ae8218a355a96d8770ac83d07bea87e13c512a",
        "af2d7e9ac9ae7e270f46ba1f975be53c09f8d875bdc8535458c2494e8a6eab251c03d0c22a56b8ca42c2063b84",
        "3853fe2b4035195a573ffc53856e77058e15d9ea064de3e59f4961d0095250ee");

    /// <summary>RFC 9180 Appendix A.2.1, DHKEM(X25519, HKDF-SHA256), HKDF-SHA256, ChaCha20Poly1305.</summary>
    internal static readonly HpkeBaseModeVector X25519ChaCha20Poly1305 = new(
        "RFC 9180 A.2.1",
        HpkeKem.DhkemX25519HkdfSha256,
        HpkeAead.ChaCha20Poly1305,
        "f4ec9b33b792c372c1d2c2063507b684ef925b8c75a42dbcbf57d63ccd381600",
        "4310ee97d88cc1f088a5576c77ab0cf5c3ac797f3d95139c6c84b5429c59662a",
        "8057991eef8f1f1af18f4a9491d16a1ce333f695d4db8e38da75975c4478e0fb",
        "1afa08d3dec047a643885163f1180476fa7ddb54c6a8029ea33f95796bf2ac4a",
        "0bbe78490412b4bbea4812666f7916932b828bba79942424abb65244930d69a7",
        "ad2744de8e17f4ebba575b3f5f5a8fa1f69c2a07f6e7500bc60ca6e3e3ec1c91",
        "5c4d98150661b848853b547f",
        "a3b010d4994890e2c6968a36f64470d3c824c8f5029942feb11e7a74b2921922",
        "1c5250d8034ec2b784ba2cfd69dbdb8af406cfe3ff938e131f0def8c8b60b4db21993c62ce81883d2dd1b51a28",
        "6b53c051e4199c518de79594e1c4ab18b96f081549d45ce015be002090bb119e85285337cc95ba5f59992dc98c",
        "4bbd6243b8bb54cec311fac9df81841b6fd61f56538a775e7c80a9f40160606e");

    /// <summary>CFRG test-vectors.json, mode 0, DHKEM(X25519, HKDF-SHA256), HKDF-SHA256, AES-256-GCM.</summary>
    internal static readonly HpkeBaseModeVector X25519Aes256Gcm = new(
        "CFRG test-vectors.json X25519 AES-256-GCM",
        HpkeKem.DhkemX25519HkdfSha256,
        HpkeAead.Aes256Gcm,
        "179d4b53b6365c45b600c4163b61d95cbc2f4d9e36f1695558dce265ab8bab11",
        "430f4b9859665145a6b1ba274024487bd66f03a2dd577d7753c68d7d7d00c00c",
        "497b4502664cfea5d5af0b39934dac72242a74f8480451e1aee7d6a53320333d",
        "6c93e09869df3402d7bf231bf540fadd35cd56be14f97178f0954db94b7fc256",
        "3101c54c3a4f87439eaac080699ed9bbcc726ffe44e860c0424ccb7e3e2ead7b",
        "f50b0609186798729ed0564b36ef2ef8044f1f9d05636874d1f46c819c7a669f",
        "151d9929e2449747889bc923",
        "86017151bbff6a1940e8abae2ac9e0e7032e33df1eaaecc02ca6259b130d62df",
        "e5d84cd531cfb583096e7cfa9641bd3079cf3a91cda813c52deb5f512be9931980a41de125a925cdad859d5b7a",
        "2c43aff25343fdbff864506f0818b9d87df84ea01b1a2144d23b4d40c26bf655fdf197fe40297a8aebeed5cc2d",
        "ded6cffafaea6b812cbf3e241e88332adbc077aca81512914213810ee291770a");

    /// <summary>RFC 9180 Appendix A.3.1, DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, AES-128-GCM.</summary>
    internal static readonly HpkeBaseModeVector P256Aes128Gcm = new(
        "RFC 9180 A.3.1",
        HpkeKem.DhkemP256HkdfSha256,
        HpkeAead.Aes128Gcm,
        "4995788ef4b9d6132b249ce59a77281493eb39af373d236a1fe415cb0c2d7beb",
        "04fe8c19ce0905191ebc298a9245792531f26f0cece2460639e8bc39cb7f706a826a779b4cf969b8a0e539c7f62fb3d30ad6aa8f80e30f1d128aafd68a2ce72ea0",
        "f3ce7fdae57e1a310d87f1ebbde6f328be0a99cdbcadf4d6589cf29de4b8ffd2",
        "04a92719c6195d5085104f469a8b9814d5838ff72b60501e2c4466e5e67b325ac98536d7b61a1af4b78e5b7f951c0900be863c403ce65c9bfcb9382657222d18c4",
        "c0d26aeab536609a572b07695d933b589dcf363ff9d93c93adea537aeabb8cb8",
        "868c066ef58aae6dc589b6cfdd18f97e",
        "4e0bc5018beba4bf004cca59",
        "14ad94af484a7ad3ef40e9f3be99ecc6fa9036df9d4920548424df127ee0d99f",
        "5ad590bb8baa577f8619db35a36311226a896e7342a6d836d8b7bcd2f20b6c7f9076ac232e3ab2523f39513434",
        "fa6f037b47fc21826b610172ca9637e82d6e5801eb31cbd3748271affd4ecb06646e0329cbdf3c3cd655b28e82",
        "5e9bc3d236e1911d95e65b576a8a86d478fb827e8bdfe77b741b289890490d4d");

    /// <summary>CFRG test-vectors.json, mode 0, DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, AES-256-GCM.</summary>
    internal static readonly HpkeBaseModeVector P256Aes256Gcm = new(
        "CFRG test-vectors.json P-256 AES-256-GCM",
        HpkeKem.DhkemP256HkdfSha256,
        HpkeAead.Aes256Gcm,
        "90345e3a1d116c1dd39ae76d95ab858c142223a63e44f8f85318cfa91a84858e",
        "04abc7e49a4c6b3566d77d0304addc6ed0e98512ffccf505e6a8e3eb25c685136f853148544876de76c0f2ef99cdc3a05ccf5ded7860c7c021238f9e2073d2356c",
        "317f915db7bc629c48fe765587897e01e282d3e8445f79f27f65d031a88082b2",
        "04c06b4f6bebc7bb495cb797ab753f911aff80aefb86fd8b6fcc35525f3ab5f03e0b21bd31a86c6048af3cb2d98e0d3bf01da5cc4c39ff5370d331a4f1f7d5a4e0",
        "48893fecd82f7c3456af6a42d8f56325d21e08c10fa81299986aaff54cde7b49",
        "ee16802a936d5f544771131900ee6973d0551de9e852ece2ef34bf0d5f9e1d1d",
        "9bc50980832a7b4b58c40161",
        "a8e9a7e62621879fdc89cea7da8e6153458f463e2851baaf009a7461d699cfb6",
        "58c61a45059d0c5704560e9d88b564a8b63f1364b8d1fcb3c4c6ddc1d291742465e902cd216f8908da49f8f96f",
        "b4e7c90d1dd62cb563694956eb517ab55d5e7d1f6366a0066c04ababaa444dbaf60a30d7bb7d3e91b969762dee",
        "7a4c2b89e1909fb0e3ca42d5040f4c2d8346dc0643d787b8474e804f8f72798e");

    /// <summary>RFC 9180 Appendix A.5.1, DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, ChaCha20Poly1305.</summary>
    internal static readonly HpkeBaseModeVector P256ChaCha20Poly1305 = new(
        "RFC 9180 A.5.1",
        HpkeKem.DhkemP256HkdfSha256,
        HpkeAead.ChaCha20Poly1305,
        "7550253e1147aae48839c1f8af80d2770fb7a4c763afe7d0afa7e0f42a5b3689",
        "04a697bffde9405c992883c5c439d6cc358170b51af72812333b015621dc0f40bad9bb726f68a5c013806a790ec716ab8669f84f6b694596c2987cf35baba2a006",
        "a4d1c55836aa30f9b3fbb6ac98d338c877c2867dd3a77396d13f68d3ab150d3b",
        "04c07836a0206e04e31d8ae99bfd549380b072a1b1b82e563c935c095827824fc1559eac6fb9e3c70cd3193968994e7fe9781aa103f5b50e934b5b2f387e381291",
        "806520f82ef0b03c823b7fc524b6b55a088f566b9751b89551c170f4113bd850",
        "a8f45490a92a3b04d1dbf6cf2c3939ad8bfc9bfcb97c04bffe116730c9dfe3fc",
        "726b4390ed2209809f58c693",
        "4f9bd9b3a8db7d7c3a5b9d44fdc1f6e37d5d77689ade5ec44a7242016e6aa205",
        "6469c41c5c81d3aa85432531ecf6460ec945bde1eb428cb2fedf7a29f5a685b4ccb0d057f03ea2952a27bb458b",
        "f1564199f7e0e110ec9c1bcdde332177fc35c1adf6e57f8d1df24022227ffa8716862dbda2b1dc546c9d114374",
        "9b13c510416ac977b553bf1741018809c246a695f45eff6d3b0356dbefe1e660");

    /// <summary>Every vector above, one data row each.</summary>
    internal static IEnumerable<object[]> All =>
    [
        [X25519Aes128Gcm],
        [X25519ChaCha20Poly1305],
        [X25519Aes256Gcm],
        [P256Aes128Gcm],
        [P256Aes256Gcm],
        [P256ChaCha20Poly1305],
    ];

    /// <summary>Sets up the sender's context of <paramref name="vector" /> with its ephemeral key.</summary>
    internal static HpkeContext SetUpSender(HpkeBaseModeVector vector)
    {
        Assert.IsTrue(Hpke.TrySetupBaseSender(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.RecipientPublicKey),
            Convert.FromHexString(vector.EphemeralPrivateKey),
            Convert.FromHexString(Info),
            new byte[Hpke.GetEncapsulatedKeySize(vector.Kem)],
            out HpkeContext? context));
        return context;
    }

    /// <summary>Sets up the recipient's context of <paramref name="vector" /> from its enc.</summary>
    internal static HpkeContext SetUpRecipient(HpkeBaseModeVector vector)
    {
        Assert.IsTrue(Hpke.TrySetupBaseRecipient(
            vector.Kem,
            HpkeKdf.HkdfSha256,
            vector.Aead,
            Convert.FromHexString(vector.EncapsulatedKey),
            Convert.FromHexString(vector.RecipientPrivateKey),
            Convert.FromHexString(Info),
            out HpkeContext? context));
        return context;
    }
}
