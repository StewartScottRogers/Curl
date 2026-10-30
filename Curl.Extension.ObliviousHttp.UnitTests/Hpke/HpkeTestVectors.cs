using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Hpke;

/// <summary>
/// RFC 9180 Appendix A.1.1 and A.2.1, the base-mode vectors for DHKEM(X25519, HKDF-SHA256)
/// and HKDF-SHA256 with AES-128-GCM and with ChaCha20Poly1305, all hexadecimal.
/// </summary>
internal static class HpkeTestVectors
{
    /// <summary>Every vector's info, "Ode on a Grecian Urn".</summary>
    internal static readonly byte[] Info = Convert.FromHexString("4f6465206f6e2061204772656369616e2055726e");

    /// <summary>Every encryption's plaintext, "Beauty is truth, truth beauty".</summary>
    internal static readonly byte[] Plaintext = Convert.FromHexString("4265617574792069732074727574682c20747275746820626561757479");

    /// <summary>The associated data of sequence number 0, "Count-0".</summary>
    internal static readonly byte[] AssociatedData0 = Convert.FromHexString("436f756e742d30");

    /// <summary>The associated data of sequence number 1, "Count-1".</summary>
    internal static readonly byte[] AssociatedData1 = Convert.FromHexString("436f756e742d31");

    /// <summary>A.1.1, AES-128-GCM.</summary>
    internal static readonly HpkeTestVector X25519Aes128Gcm = new(
        "RFC 9180 A.1.1",
        new HpkeSuite(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm),
        "52c4a758a802cd8b936eceea314432798d5baf2d7e9235dc084ab1b9cfa2f736",
        "3948cfe0ad1ddb695d780e59077195da6c56506b027329794ab02bca80815c4d",
        "4612c550263fc8ad58375df3f557aac531d26850903e55a9f23f21d8534e8ac8",
        "37fda3567bdbd628e88668c3c8d7e97d1d1253b6d4ea6d44c150f741f1bf4431",
        "f938558b5d72f1a23810b4be2ab4f84331acc02fc97babc53a52ae8218a355a96d8770ac83d07bea87e13c512a",
        "af2d7e9ac9ae7e270f46ba1f975be53c09f8d875bdc8535458c2494e8a6eab251c03d0c22a56b8ca42c2063b84",
        "3853fe2b4035195a573ffc53856e77058e15d9ea064de3e59f4961d0095250ee");

    /// <summary>A.2.1, ChaCha20Poly1305.</summary>
    internal static readonly HpkeTestVector X25519ChaCha20Poly1305 = new(
        "RFC 9180 A.2.1",
        new HpkeSuite(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.ChaCha20Poly1305),
        "f4ec9b33b792c372c1d2c2063507b684ef925b8c75a42dbcbf57d63ccd381600",
        "4310ee97d88cc1f088a5576c77ab0cf5c3ac797f3d95139c6c84b5429c59662a",
        "8057991eef8f1f1af18f4a9491d16a1ce333f695d4db8e38da75975c4478e0fb",
        "1afa08d3dec047a643885163f1180476fa7ddb54c6a8029ea33f95796bf2ac4a",
        "1c5250d8034ec2b784ba2cfd69dbdb8af406cfe3ff938e131f0def8c8b60b4db21993c62ce81883d2dd1b51a28",
        "6b53c051e4199c518de79594e1c4ab18b96f081549d45ce015be002090bb119e85285337cc95ba5f59992dc98c",
        "4bbd6243b8bb54cec311fac9df81841b6fd61f56538a775e7c80a9f40160606e");

    /// <summary>Both vectors, one data row each.</summary>
    internal static IEnumerable<object[]> All =>
    [
        [X25519Aes128Gcm],
        [X25519ChaCha20Poly1305],
    ];

    /// <summary>The vector's sender, set up with its ephemeral key.</summary>
    internal static HpkeSender SetUpSender(HpkeTestVector vector) =>
        HpkeSender.SetUp(vector.Suite, Convert.FromHexString(vector.RecipientPublicKey), Convert.FromHexString(vector.EphemeralPrivateKey), Info);

    /// <summary>The vector's receiver, set up with its encapsulated key.</summary>
    internal static HpkeReceiver SetUpReceiver(HpkeTestVector vector) =>
        HpkeReceiver.SetUp(vector.Suite, Convert.FromHexString(vector.EncapsulatedKey), Convert.FromHexString(vector.RecipientPrivateKey), Info);
}
