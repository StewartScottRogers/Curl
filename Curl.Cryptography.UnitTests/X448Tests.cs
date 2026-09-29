namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="X448" /> to RFC 7748's published vectors (section 5.2, section 6.2)
/// and to the all-zero result a low-order peer key produces (ADR-0118).
/// </summary>
[TestClass]
public sealed class X448Tests
{
    private const string AlicePrivateKey = "9a8f4925d1519f5775cf46b04b5800d4ee9ee8bae8bc5565d498c28dd9c9baf574a9419744897391006382a6f127ab1d9ac2d8c0a598726b";
    private const string AlicePublicKey = "9b08f7cc31b7e3e67d22d5aea121074a273bd2b83de09c63faa73d2c22c5d9bbc836647241d953d40c5b12da88120d53177f80e532c41fa0";
    private const string BobPrivateKey = "1c306a7ac2a0e2e0990b294470cba339e6453772b075811d8fad0d1d6927c120bb5ee8972b0d3e21374c9c921b09d1b0366f10b65173992d";
    private const string BobPublicKey = "3eb7a829b0cd20f5bcfc0b599b6feccf6da4627107bdb0d4f345b43027d8b972fc3e34fb4232a13ca706dcb57aec3dae07bdc1c67bf33609";
    private const string AliceAndBobSharedSecret = "07fff4181ac6cc95ec1c16a94a0f74d12da232ce40a77552281d282bb60c0b56fd2464c335543936521c24403085d59a449a5037514a879d";

    // RFC 7748 section 5.2, the two X448 input/output vectors.
    [TestMethod]
    [DataRow(
        "3d262fddf9ec8e88495266fea19a34d28882acef045104d0d1aae121700a779c984c24f8cdd78fbff44943eba368f54b29259a4f1c600ad3",
        "06fce640fa3487bfda5f6cf2d5263f8aad88334cbd07437f020f08f9814dc031ddbdc38c19c6da2583fa5429db94ada18aa7a7fb4ef8a086",
        "ce3e4ff95a60dc6697da1db1d85e6afbdf79b50a2412d7546d5f239fe14fbaadeb445fc66a01b0779d98223961111e21766282f73dd96b6f")]
    [DataRow(
        "203d494428b8399352665ddca42f9de8fef600908e0d461cb021f8c538345dd77c3e4806e25f46d3315c44e0a5b4371282dd2c8d5be3095f",
        "0fbcc2f993cd56d3305b0b7d9e55d4c1a8fb5dbb52f8e9a1e9b6201b165d015894e56c4d3570bee52fe205e28a78b91cdfbde71ce8d157db",
        "884a02576239ff7a2f2f63b2db6a9ff37047ac13568e1e30fe63c4a7ad1b3ee3a5700df34321d62077e63633c575c1c954514e99da7c179d")]
    public void TryComputeSharedSecret_Rfc7748Section52Vector_GivesTheExpectedUCoordinate(
        string scalar,
        string uCoordinate,
        string expected)
    {
        byte[] result = new byte[X448.KeySize];

        bool succeeded = X448.TryComputeSharedSecret(Convert.FromHexString(scalar), Convert.FromHexString(uCoordinate), result);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    // RFC 7748 section 5.2, the iterated vectors: k = u = 5, then k, u = X448(k, u), k.
    [TestMethod]
    [DataRow(1, "3f482c8a9f19b01e6c46ee9711d9dc14fd4bf67af30765c2ae2b846a4d23a8cd0db897086239492caf350b51f833868b9bc2b3bca9cf4113")]
    [DataRow(1_000, "aa3b4749d55b9daf1e5b00288826c467274ce3ebbdd5c17b975e09d4af6c67cf10d087202db88286e2b79fceea3ec353ef54faa26e219f38")]
    public void TryComputeSharedSecret_Rfc7748Section52Iterations_GiveTheExpectedK(int iterations, string expected)
    {
        Assert.AreEqual(expected, Iterate(iterations));
    }

    // RFC 7748 section 5.2, the 1,000,000 iteration vector; hours, not milliseconds.
    [TestMethod]
    [TestCategory("Integration")]
    public void TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK()
    {
        Assert.AreEqual(
            "077f453681caca3693198420bbe515cae0002472519b3e67661a7e89cab94695c8f4bcd66e61b9b9c946da8d524de3d69bd9d9d66b997e37",
            Iterate(1_000_000));
    }

    // RFC 7748 section 6.2.
    [TestMethod]
    [DataRow(AlicePrivateKey, AlicePublicKey)]
    [DataRow(BobPrivateKey, BobPublicKey)]
    public void ComputePublicKey_Rfc7748Section62Key_GivesThePublishedPublicKey(string privateKey, string expected)
    {
        byte[] publicKey = new byte[X448.KeySize];

        X448.ComputePublicKey(Convert.FromHexString(privateKey), publicKey);

        Assert.AreEqual(expected, Convert.ToHexStringLower(publicKey));
    }

    // RFC 7748 section 6.2: X448(a, K_B) = X448(b, K_A) = K.
    [TestMethod]
    [DataRow(AlicePrivateKey, BobPublicKey)]
    [DataRow(BobPrivateKey, AlicePublicKey)]
    public void TryComputeSharedSecret_Rfc7748Section62Keys_GiveThePublishedSharedSecret(string privateKey, string peerPublicKey)
    {
        byte[] sharedSecret = new byte[X448.KeySize];

        bool succeeded = X448.TryComputeSharedSecret(Convert.FromHexString(privateKey), Convert.FromHexString(peerPublicKey), sharedSecret);

        Assert.IsTrue(succeeded);
        Assert.AreEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    // Low-order u-coordinates (RFC 7748 section 7): 0, 1 and p - 1, and the non-canonical
    // encodings p and p + 1 of 0 and 1, which the function reduces modulo
    // p = 2^448 - 2^224 - 1.
    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("fefffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    [DataRow("fffffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    [DataRow("00000000000000000000000000000000000000000000000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public void TryComputeSharedSecret_LowOrderPeerKey_ReturnsFalseWithAnAllZeroSecret(string peerPublicKey)
    {
        byte[] sharedSecret = [.. Enumerable.Repeat((byte)0xAA, X448.KeySize)];

        bool succeeded = X448.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), Convert.FromHexString(peerPublicKey), sharedSecret);

        Assert.IsFalse(succeeded);
        CollectionAssert.AreEqual(new byte[X448.KeySize], sharedSecret);
    }

    [TestMethod]
    public void TryComputeSharedSecret_FlippedPeerKeyBit_GivesADifferentSecret()
    {
        byte[] peerPublicKey = Convert.FromHexString(BobPublicKey);
        peerPublicKey[0] ^= 0x01;
        byte[] sharedSecret = new byte[X448.KeySize];

        X448.TryComputeSharedSecret(Convert.FromHexString(AlicePrivateKey), peerPublicKey, sharedSecret);

        Assert.AreNotEqual(AliceAndBobSharedSecret, Convert.ToHexStringLower(sharedSecret));
    }

    [TestMethod]
    public void GeneratePrivateKey_TwoKeys_AgreeOnASharedSecret()
    {
        byte[] alice = new byte[X448.KeySize];
        byte[] bob = new byte[X448.KeySize];
        X448.GeneratePrivateKey(alice);
        X448.GeneratePrivateKey(bob);
        byte[] alicePublic = new byte[X448.KeySize];
        byte[] bobPublic = new byte[X448.KeySize];
        X448.ComputePublicKey(alice, alicePublic);
        X448.ComputePublicKey(bob, bobPublic);
        byte[] aliceSecret = new byte[X448.KeySize];
        byte[] bobSecret = new byte[X448.KeySize];

        Assert.IsTrue(X448.TryComputeSharedSecret(alice, bobPublic, aliceSecret));
        Assert.IsTrue(X448.TryComputeSharedSecret(bob, alicePublic, bobSecret));
        CollectionAssert.AreNotEqual(alice, bob);
        CollectionAssert.AreEqual(aliceSecret, bobSecret);
    }

    [TestMethod]
    [DataRow(55)]
    [DataRow(57)]
    public void GeneratePrivateKey_WrongLength_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => X448.GeneratePrivateKey(new byte[length]));
    }

    [TestMethod]
    [DataRow(32, 56)]
    [DataRow(56, 55)]
    public void ComputePublicKey_WrongLength_Throws(int privateKeyLength, int publicKeyLength)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => X448.ComputePublicKey(new byte[privateKeyLength], new byte[publicKeyLength]));
    }

    [TestMethod]
    [DataRow(55, 56, 56)]
    [DataRow(56, 32, 56)]
    [DataRow(56, 56, 0)]
    public void TryComputeSharedSecret_WrongLength_Throws(int privateKeyLength, int peerPublicKeyLength, int sharedSecretLength)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => X448.TryComputeSharedSecret(new byte[privateKeyLength], new byte[peerPublicKeyLength], new byte[sharedSecretLength]));
    }

    private static string Iterate(int iterations)
    {
        byte[] k = new byte[X448.KeySize];
        k[0] = 5;
        byte[] u = (byte[])k.Clone();
        byte[] next = new byte[X448.KeySize];
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            X448.TryComputeSharedSecret(k, u, next);
            (u, k, next) = (k, next, u);
        }

        return Convert.ToHexStringLower(k);
    }
}
