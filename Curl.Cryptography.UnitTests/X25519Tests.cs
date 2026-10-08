using Curl.Testing;

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

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] result = new byte[X25519.KeySize];
        diagnostics.Arrange("source", "RFC 7748 section 5.2 input/output vector");
        diagnostics.Bytes("scalar", Convert.FromHexString(scalar));
        diagnostics.Bytes("u-coordinate", Convert.FromHexString(uCoordinate));

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(scalar), Convert.FromHexString(uCoordinate), result);
        diagnostics.Act("succeeded", succeeded);
        diagnostics.Bytes("output u-coordinate", result);

        diagnostics.Assert("succeeded", true, succeeded);
        diagnostics.Diff("output u-coordinate", expected, Convert.ToHexStringLower(result));
        Assert.IsTrue(succeeded);
        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    // RFC 7748 section 5.2, the iterated vectors: k = u = 9, then k, u = X25519(k, u), k.
    [TestMethod]
    [DataRow(1, "422c8e7a6227d7bca1350b3e2bb7279f7897b87bb6854b783c60e80311ae3079")]
    [DataRow(1_000, "684cf59ba83309552800ef566f2f4d3c1c3887c49360e3875f2eb94d99532c51")]
    public void TryComputeSharedSecret_Rfc7748Section52Iterations_GiveTheExpectedK(int iterations, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("source", "RFC 7748 section 5.2 iterated vector, starting from k = u = 9");
        diagnostics.Arrange("iterations", iterations);

        string k;
        using (diagnostics.Phase("iterate"))
        {
            k = Iterate(iterations);
        }

        diagnostics.Act("k", k);

        diagnostics.Diff("k", expected, k);
        Assert.AreEqual(expected, k);
    }

    // RFC 7748 section 5.2, the 1,000,000 iteration vector; minutes, not milliseconds.
    [TestMethod]
    [TestCategory("Integration")]
    public void TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("source", "RFC 7748 section 5.2 iterated vector, starting from k = u = 9");
        diagnostics.Arrange("iterations", 1_000_000);

        string k;
        using (diagnostics.Phase("iterate"))
        {
            k = Iterate(1_000_000);
        }

        diagnostics.Act("k", k);

        diagnostics.Diff("k", "7c3911e0ab2586fd864497297e575e6f3bc601c0883c30df5f4dd2d24f665424", k);
        Assert.AreEqual("7c3911e0ab2586fd864497297e575e6f3bc601c0883c30df5f4dd2d24f665424", k);
    }

    // RFC 7748 section 6.1.
    [TestMethod]
    [DataRow(AlicePrivateKey, AlicePublicKey)]
    [DataRow(BobPrivateKey, BobPublicKey)]
    public void ComputePublicKey_Rfc7748Section61Key_GivesThePublishedPublicKey(string privateKey, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] publicKey = new byte[X25519.KeySize];
        diagnostics.Arrange("source", "RFC 7748 section 6.1");
        diagnostics.Bytes("private key", Convert.FromHexString(privateKey));

        X25519.ComputePublicKey(Convert.FromHexString(privateKey), publicKey);
        diagnostics.Act("public key", Convert.ToHexStringLower(publicKey));

        diagnostics.Diff("public key", expected, Convert.ToHexStringLower(publicKey));
        Assert.AreEqual(expected, Convert.ToHexStringLower(publicKey));
    }

    // RFC 7748 section 6.1: X25519(a, K_B) = X25519(b, K_A) = K.
    [TestMethod]
    [DataRow(AlicePrivateKey, BobPublicKey)]
    [DataRow(BobPrivateKey, AlicePublicKey)]
    public void TryComputeSharedSecret_Rfc7748Section61Keys_GiveThePublishedSharedSecret(string privateKey, string peerPublicKey)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] sharedSecret = new byte[X25519.KeySize];
        diagnostics.Arrange("source", "RFC 7748 section 6.1");
        diagnostics.Bytes("private key", Convert.FromHexString(privateKey));
        diagnostics.Bytes("peer public key", Convert.FromHexString(peerPublicKey));

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(privateKey), Convert.FromHexString(peerPublicKey), sharedSecret);
        diagnostics.Act("succeeded", succeeded);
        diagnostics.Act("shared secret", Convert.ToHexString(sharedSecret));

        diagnostics.Assert("succeeded", true, succeeded);
        diagnostics.Diff("shared secret", AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
        Assert.IsTrue(succeeded);
        Assert.AreEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    // Every small-order u-coordinate encoding, from libsodium's blacklist in
    // crypto_scalarmult/curve25519/ref10/x25519_ref10.c (has_small_order): each gives
    // the all-zero output that RFC 7748 section 6.1 says a protocol may check for.
    // In order: 0 (order 4), 1 (order 1), the two order-8 points, p - 1 (order 2),
    // and the non-canonical p (that is 0) and p + 1 (that is 1), which the function
    // reduces modulo p = 2^255 - 19. RFC 7748 section 5 masks the top bit, so the
    // last seven rows, the same encodings with bit 255 set (as Wycheproof's
    // x25519_test.json feeds them), must give the same result.
    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("e0eb7a7c3b41b8ae1656e3faf19fc46ada098deb9c32b1fd866205165f49b800")]
    [DataRow("5f9c95bca3508c24b1d0b1559c83ef5b04445cc4581c8e86d8224eddd09f1157")]
    [DataRow("ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000080")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000080")]
    [DataRow("e0eb7a7c3b41b8ae1656e3faf19fc46ada098deb9c32b1fd866205165f49b880")]
    [DataRow("5f9c95bca3508c24b1d0b1559c83ef5b04445cc4581c8e86d8224eddd09f11d7")]
    [DataRow("ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    [DataRow("eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public void TryComputeSharedSecret_LowOrderPeerKey_ReturnsFalseWithAnAllZeroSecret(string peerPublicKey)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] sharedSecret = [.. Enumerable.Repeat((byte)0xAA, X25519.KeySize)];
        diagnostics.Arrange("source", "libsodium x25519_ref10.c has_small_order list, RFC 7748 section 6.1 all-zero check");
        diagnostics.Bytes("private key", Convert.FromHexString(AlicePrivateKey));
        diagnostics.Bytes("peer public key", Convert.FromHexString(peerPublicKey));

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), Convert.FromHexString(peerPublicKey), sharedSecret);
        diagnostics.Act("succeeded", succeeded);

        diagnostics.Assert("succeeded", false, succeeded);
        diagnostics.Diff("shared secret", new byte[X25519.KeySize], sharedSecret);
        Assert.IsFalse(succeeded);
        CollectionAssert.AreEqual(new byte[X25519.KeySize], sharedSecret);
    }

    // RFC 7748 section 5: the top bit of the u-coordinate is masked, so setting it
    // changes nothing.
    [TestMethod]
    public void TryComputeSharedSecret_PeerKeyWithTopBitSet_IgnoresTheTopBit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] peerPublicKey = Convert.FromHexString(BobPublicKey);
        peerPublicKey[X25519.KeySize - 1] |= 0x80;
        byte[] sharedSecret = new byte[X25519.KeySize];
        diagnostics.Arrange("source", "RFC 7748 section 6.1 keys, section 5 top-bit masking");
        diagnostics.Bytes("private key", Convert.FromHexString(AlicePrivateKey));
        diagnostics.Bytes("peer public key", peerPublicKey);

        X25519.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), peerPublicKey, sharedSecret);
        diagnostics.Act("shared secret", Convert.ToHexString(sharedSecret));

        diagnostics.Diff("shared secret", AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
        Assert.AreEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    [TestMethod]
    public void TryComputeSharedSecret_FlippedPeerKeyBit_GivesADifferentSecret()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] peerPublicKey = Convert.FromHexString(BobPublicKey);
        peerPublicKey[0] ^= 0x01;
        byte[] sharedSecret = new byte[X25519.KeySize];
        diagnostics.Arrange("source", "RFC 7748 section 6.1 keys, bit 0 of Bob's public key flipped");
        diagnostics.Bytes("private key", Convert.FromHexString(AlicePrivateKey));
        diagnostics.Bytes("peer public key", peerPublicKey);

        X25519.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), peerPublicKey, sharedSecret);
        diagnostics.Act("shared secret", Convert.ToHexString(sharedSecret));

        diagnostics.Diff("shared secret against the published one", AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
        Assert.AreNotEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    [TestMethod]
    public void GeneratePrivateKey_TwoKeys_AgreeOnASharedSecret()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
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
        diagnostics.Arrange("source", "two private keys from the system random number generator");

        bool aliceSucceeded = X25519.TryComputeSharedSecret(alice, bobPublic, aliceSecret);
        bool bobSucceeded = X25519.TryComputeSharedSecret(bob, alicePublic, bobSecret);
        diagnostics.Act("Alice succeeded", aliceSucceeded);
        diagnostics.Act("Bob succeeded", bobSucceeded);

        diagnostics.Assert("private keys differ", true, !alice.AsSpan().SequenceEqual(bob));
        diagnostics.Diff("shared secret", aliceSecret, bobSecret);
        Assert.IsTrue(aliceSucceeded);
        Assert.IsTrue(bobSucceeded);
        CollectionAssert.AreNotEqual(alice, bob);
        CollectionAssert.AreEqual(aliceSecret, bobSecret);
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void GeneratePrivateKey_WrongLength_Throws(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("private key length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => X25519.GeneratePrivateKey(new byte[length]));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(31, 32)]
    [DataRow(32, 31)]
    public void ComputePublicKey_WrongLength_Throws(int privateKeyLength, int publicKeyLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("private key length", privateKeyLength);
        diagnostics.Arrange("public key length", publicKeyLength);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => X25519.ComputePublicKey(new byte[privateKeyLength], new byte[publicKeyLength]));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(31, 32, 32)]
    [DataRow(32, 33, 32)]
    [DataRow(32, 32, 0)]
    public void TryComputeSharedSecret_WrongLength_Throws(int privateKeyLength, int peerPublicKeyLength, int sharedSecretLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("private key length", privateKeyLength);
        diagnostics.Arrange("peer public key length", peerPublicKeyLength);
        diagnostics.Arrange("shared secret length", sharedSecretLength);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => X25519.TryComputeSharedSecret(new byte[privateKeyLength], new byte[peerPublicKeyLength], new byte[sharedSecretLength]));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
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
