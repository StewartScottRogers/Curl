namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="X25519" /> to RFC 7748's published vectors (section 5.2, section 6.1)
/// and to the all-zero result a low-order peer key produces (ADR-0118).
/// </summary>
[TestClass]
public sealed class X25519Tests
{
    private const string AlicePrivateKey = "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a";
    private const string AlicePublicKey = "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a";
    private const string BobPrivateKey = "5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb";
    private const string BobPublicKey = "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f";
    private const string AliceAndBobSharedSecret = "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742";

    // RFC 7748 section 5.2, the two X25519 input/output vectors.
    [TestMethod]
    [DataRow(
        "a546e36bf0527c9d3b16154b82465edd62144c0ac1fc5a18506a2244ba449ac4",
        "e6db6867583030db3594c1a424b15f7c726624ec26b3353b10a903a6d0ab1c4c",
        "c3da55379de9c6908e94ea4df28d084f32eccf03491c71f754b4075577a28552")]
    [DataRow(
        "4b66e9d4d1b4673c5ad22691957d6af5c11b6421e0ea01d42ca4169e7918ba0d",
        "e5210f12786811d3f4b7959d0538ae2c31dbe7106fc03c3efc4cd549c715a493",
        "95cbde9476e8907d7aade45cb4b873f88b595a68799fa152e6f8f7647aac7957")]
    public void TryComputeSharedSecret_Rfc7748Section52Vector_GivesTheExpectedUCoordinate(
        string scalar,
        string uCoordinate,
        string expected)
    {
        byte[] result = new byte[X25519.KeySize];

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(scalar), Convert.FromHexString(uCoordinate), result);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    // RFC 7748 section 5.2, the iterated vectors: k = u = 9, then k, u = X25519(k, u), k.
    [TestMethod]
    [DataRow(1, "422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079")]
    [DataRow(1_000, "684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51")]
    public void TryComputeSharedSecret_Rfc7748Section52Iterations_GiveTheExpectedK(int iterations, string expected)
    {
        Assert.AreEqual(expected, Iterate(iterations));
    }

    // RFC 7748 section 5.2, the 1,000,000 iteration vector; minutes, not milliseconds.
    [TestMethod]
    [TestCategory("Integration")]
    public void TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK()
    {
        Assert.AreEqual("7c3911e0ab2586fd864497297e575e6f3bc601c0883c30df5f4dd2d24f665424", Iterate(1_000_000));
    }

    // RFC 7748 section 6.1.
    [TestMethod]
    [DataRow(AlicePrivateKey, AlicePublicKey)]
    [DataRow(BobPrivateKey, BobPublicKey)]
    public void ComputePublicKey_Rfc7748Section61Key_GivesThePublishedPublicKey(string privateKey, string expected)
    {
        byte[] publicKey = new byte[X25519.KeySize];

        X25519.ComputePublicKey(Convert.FromHexString(privateKey), publicKey);

        Assert.AreEqual(expected, Convert.ToHexStringLower(publicKey));
    }

    // RFC 7748 section 6.1: X25519(a, K_B) = X25519(b, K_A) = K.
    [TestMethod]
    [DataRow(AlicePrivateKey, BobPublicKey)]
    [DataRow(BobPrivateKey, AlicePublicKey)]
    public void TryComputeSharedSecret_Rfc7748Section61Keys_GiveThePublishedSharedSecret(string privateKey, string peerPublicKey)
    {
        byte[] sharedSecret = new byte[X25519.KeySize];

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(privateKey), Convert.FromHexString(peerPublicKey), sharedSecret);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    // Low-order u-coordinates (RFC 7748 section 7): 0, 1, and their non-canonical
    // encodings p and p + 1, which the function reduces modulo p = 2^255 - 19.
    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    public void TryComputeSharedSecret_LowOrderPeerKey_ReturnsFalseWithAnAllZeroSecret(string peerPublicKey)
    {
        byte[] sharedSecret = [.. Enumerable.Repeat((byte)0xAA, X25519.KeySize)];

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), Convert.FromHexString(peerPublicKey), sharedSecret);

        Assert.IsFalse(succeeded);
        CollectionAssert.AreEqual(new byte[X25519.KeySize], sharedSecret);
    }

    // RFC 7748 section 5: the top bit of the u-coordinate is masked, so setting it
    // changes nothing.
    [TestMethod]
    public void TryComputeSharedSecret_PeerKeyWithTopBitSet_IgnoresTheTopBit()
    {
        byte[] peerPublicKey = Convert.FromHexString(BobPublicKey);
        peerPublicKey[X25519.KeySize - 1] |= 0x80;
        byte[] sharedSecret = new byte[X25519.KeySize];

        X25519.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), peerPublicKey, sharedSecret);

        Assert.AreEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    [TestMethod]
    public void TryComputeSharedSecret_FlippedPeerKeyBit_GivesADifferentSecret()
    {
        byte[] peerPublicKey = Convert.FromHexString(BobPublicKey);
        peerPublicKey[0] ^= 0x01;
        byte[] sharedSecret = new byte[X25519.KeySize];

        X25519.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), peerPublicKey, sharedSecret);

        Assert.AreNotEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    [TestMethod]
    public void GeneratePrivateKey_TwoKeys_AgreeOnASharedSecret()
    {
        byte[] alice = new byte[X25519.KeySize];
        byte[] bob = new byte[X25519.KeySize];
        X25519.GeneratePrivateKey(alice);
        X25519.GeneratePrivateKey(bob);
        byte[] alicePublic = new byte[X25519.KeySize];
        byte[] bobPublic = new byte[X25519.KeySize];
        X25519.ComputePublicKey(alice, alicePublic);
        X25519.ComputePublicKey(bob, bobPublic);
        byte[] aliceSecret = new byte[X25519.KeySize];
        byte[] bobSecret = new byte[X25519.KeySize];

        Assert.IsTrue(X25519.TryComputeSharedSecret(alice, bobPublic, aliceSecret));
        Assert.IsTrue(X25519.TryComputeSharedSecret(bob, alicePublic, bobSecret));
        CollectionAssert.AreNotEqual(alice, bob);
        CollectionAssert.AreEqual(aliceSecret, bobSecret);
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void GeneratePrivateKey_WrongLength_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => X25519.GeneratePrivateKey(new byte[length]));
    }

    [TestMethod]
    [DataRow(31, 32)]
    [DataRow(32, 31)]
    public void ComputePublicKey_WrongLength_Throws(int privateKeyLength, int publicKeyLength)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => X25519.ComputePublicKey(new byte[privateKeyLength], new byte[publicKeyLength]));
    }

    [TestMethod]
    [DataRow(31, 32, 32)]
    [DataRow(32, 33, 32)]
    [DataRow(32, 32, 0)]
    public void TryComputeSharedSecret_WrongLength_Throws(int privateKeyLength, int peerPublicKeyLength, int sharedSecretLength)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => X25519.TryComputeSharedSecret(new byte[privateKeyLength], new byte[peerPublicKeyLength], new byte[sharedSecretLength]));
    }

    private static string Iterate(int iterations)
    {
        byte[] k = new byte[X25519.KeySize];
        k[0] = 9;
        byte[] u = (byte[])k.Clone();
        byte[] next = new byte[X25519.KeySize];
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            X25519.TryComputeSharedSecret(k, u, next);
            (u, k, next) = (k, next, u);
        }

        return Convert.ToHexStringLower(k);
    }
}
