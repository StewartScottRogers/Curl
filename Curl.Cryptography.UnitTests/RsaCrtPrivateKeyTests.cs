using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="RsaCrtPrivateKey" /> to the RSASP1 results in PKCS #1 v2.1's published
/// RSA-PSS test vectors (<c>pss-vect.txt</c>, the second draft's test data from RSA
/// Laboratories, as pyca/cryptography redistributes it under
/// <c>vectors/cryptography_vectors/asymmetric/RSA/pkcs-1v2-1d2-vec</c>), and to the
/// non-CRT m^d mod n.
/// </summary>
[TestClass]
public sealed class RsaCrtPrivateKeyTests
{
    // pss-vect.txt Example 1: a 1024-bit key, and the signature of PSS Example 1.1.
    private static readonly RSAParameters Example1 = new()
    {
        Modulus = Convert.FromHexString(
            "a56e4a0e701017589a5187dc7ea841d156f2ec0e36ad52a44dfeb1e61f7ad991d8c51056ffedb162b4c0f283a12a88a394dff526ab7291cbb307ceabfce0b1df"
            + "d5cd9508096d5b2b8b6df5d671ef6377c0921cb23c270a70e2598e6ff89d19f105acc2d3f0cb35f29280e1386b6f64c4ef22e1e1f20d0ce8cffb2249bd9a2137"),
        Exponent = [0x01, 0x00, 0x01],
        D = Convert.FromHexString(
            "33a5042a90b27d4f5451ca9bbbd0b44771a101af884340aef9885f2a4bbe92e894a724ac3c568c8f97853ad07c0266c8c6a3ca0929f1e8f11231884429fc4d9a"
            + "e55fee896a10ce707c3ed7e734e44727a39574501a532683109c2abacaba283c31b4bd2f53c3ee37e352cee34f9e503bd80c0622ad79c6dcee883547c6a3b325"),
        P = Convert.FromHexString("e7e8942720a877517273a356053ea2a1bc0c94aa72d55c6e86296b2dfc967948c0a72cbccca7eacb35706e09a1df55a1535bd9b3cc34160b3b6dcd3eda8e6443"),
        Q = Convert.FromHexString("b69dca1cf7d4d7ec81e75b90fcca874abcde123fd2700180aa90479b6e48de8d67ed24f9f19d85ba275874f542cd20dc723e6963364a1f9425452b269a6799fd"),
        DP = Convert.FromHexString("28fa13938655be1f8a159cbaca5a72ea190c30089e19cd274a556f36c4f6e19f554b34c077790427bbdd8dd3ede2448328f385d81b30e8e43b2fffa027861979"),
        DQ = Convert.FromHexString("1a8b38f398fa712049898d7fb79ee0a77668791299cdfa09efc0e507acb21ed74301ef5bfd48be455eaeb6e1678255827580a8e4e8e14151d1510a82a3f2e729"),
        InverseQ = Convert.FromHexString("27156aba4126d24a81f3a528cbfb27f56886f840a9f6e86e17a44b94fe9319584b8e22fdde1e5a2e3bd8aa5ba8d8584194eb2190acf832b847f13a3d24a79f4d"),
    };

    private const string Example1Signature =
        "9074308fb598e9701b2294388e52f971faac2b60a5145af185df5287b5ed2887e57ce7fd44dc8634e407c8e0e4360bc226f3ec227f9d9e54638e8d31f5051215"
        + "df6ebb9c2f9579aa77598a38f914b5b9c1bd83c4e2f9f382a0d0aa3542ffee65984a601bc69eb28deb27dca12c82c2d4c3f66cd500f1ff2b994d8a4e30cbb33c";

    // pss-vect.txt Example 2: a 1025-bit key, 129 bytes, primes of 65 bytes, and the signature of PSS Example 2.1.
    private static readonly RSAParameters Example2 = new()
    {
        Modulus = Convert.FromHexString(
            "01d40c1bcf97a68ae7cdbd8a7bf3e34fa19dcca4ef75a47454375f94514d88fed006fb829f8419ff87d6315da68a1ff3a0938e9abb3464011c303ad99199cf0c7c"
            + "7a8b477dce829e8844f625b115e5e9c4a59cf8f8113b6834336a2fd2689b472cbb5e5cabe674350c59b6c17e176874fb42f8fc3d176a017edc61fd326c4b33c9"),
        Exponent = [0x01, 0x00, 0x01],
        D = Convert.FromHexString(
            "027d147e4673057377fd1ea201565772176a7dc38358d376045685a2e787c23c15576bc16b9f444402d6bfc5d98a3e88ea13ef67c353eca0c0ddba9255bd7b8b"
            + "b50a644afdfd1dd51695b252d22e7318d1b6687a1c10ff75545f3db0fe602d5f2b7f294e3601eab7b9d1cecd767f64692e3e536ca2846cb0c2dd486a39fa75b1"),
        P = Convert.FromHexString("016601e926a0f8c9e26ecab769ea65a5e7c52cc9e080ef519457c644da6891c5a104d3ea7955929a22e7c68a7af9fcad777c3ccc2b9e3d3650bce404399b7e59d1"),
        Q = Convert.FromHexString("014eafa1d4d0184da7e31f877d1281ddda625664869e8379e67ad3b75eae74a580e9827abd6eb7a002cb5411f5266797768fb8e95ae40e3e8a01f35ff89e56c079"),
        DP = Convert.FromHexString("e247cce504939b8f0a36090de200938755e2444b29539a7da7a902f6056835c0db7b52559497cfe2c61a8086d0213c472c78851800b171f6401de2e9c2756f31"),
        DQ = Convert.FromHexString("b12fba757855e586e46f64c38a70c68b3f548d93d787b399999d4c8f0bbd2581c21e19ed0018a6d5d3df86424b3abcad40199d31495b61309f27c1bf55d487c1"),
        InverseQ = Convert.FromHexString("564b1e1fa003bda91e89090425aac05b91da9ee25061e7628d5f51304a84992fdc33762bd378a59f030a334d532bd0dae8f298ea9ed844636ad5fb8cbdc03cad"),
    };

    private const string Example2Signature =
        "014c5ba5338328ccc6e7a90bf1c0ab3fd606ff4796d3c12e4b639ed9136a5fec6c16d8884bdd99cfdc521456b0742b736868cf90de099adb8d5ffd1deff39ba4"
        + "007ab746cefdb22d7df0e225f54627dc65466131721b90af445363a8358b9f607642f78fab0ab0f43b7168d64bae70d8827848d8ef1e421c5754ddf42c2589b5b3";

    [TestMethod]
    [DataRow(1, DisplayName = "pss-vect Example 1.1, 1024 bits")]
    [DataRow(2, DisplayName = "pss-vect Example 2.1, 1025 bits")]
    public void ApplyPrivateExponent_PublishedEncodedMessage_GivesThePublishedSignature(int example)
    {
        RSAParameters parameters = example == 1 ? Example1 : Example2;
        byte[] expected = Convert.FromHexString(example == 1 ? Example1Signature : Example2Signature);
        using var key = new RsaCrtPrivateKey(parameters);
        byte[] encodedMessage = ApplyPublicExponent(parameters, expected);
        byte[] signature = new byte[key.ModulusLength];

        key.ApplyPrivateExponent(encodedMessage, signature);

        Assert.AreEqual(Convert.ToHexString(expected), Convert.ToHexString(signature));
    }

    [TestMethod]
    public void ApplyPrivateExponent_AnyBlindingRandom_GivesTheNonCrtPowerModN()
    {
        using RSA rsa = RSA.Create(2048);
        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: true);
        using var key = new RsaCrtPrivateKey(parameters);
        byte[] message = RandomNumberGenerator.GetBytes(key.ModulusLength);
        message[0] = 0;
        BigInteger expected = BigInteger.ModPow(ToInteger(message), ToInteger(parameters.D!), ToInteger(parameters.Modulus!));

        foreach (byte fill in new byte[] { 0x01, 0x5a, 0xff })
        {
            byte[] blindingRandom = new byte[key.BlindingRandomLength];
            Array.Fill(blindingRandom, fill);
            byte[] signature = new byte[key.ModulusLength];

            key.ApplyPrivateExponent(message, blindingRandom, signature);

            Assert.AreEqual(expected, ToInteger(signature), $"blinding byte 0x{fill:x2}");
        }
    }

    [TestMethod]
    public void ApplyPrivateExponent_MessageOfZero_GivesZero()
    {
        using var key = new RsaCrtPrivateKey(Example1);
        byte[] signature = new byte[key.ModulusLength];
        Array.Fill(signature, (byte)0xaa);

        key.ApplyPrivateExponent(new byte[key.ModulusLength], signature);

        Assert.IsTrue(signature.All(value => value == 0));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "m = n")]
    [DataRow(true, DisplayName = "m = 2^1024 - 1")]
    public void ApplyPrivateExponent_MessageNotBelowTheModulus_ThrowsArgumentException(bool allOnes)
    {
        using var key = new RsaCrtPrivateKey(Example1);
        byte[] message = allOnes ? Enumerable.Repeat((byte)0xff, key.ModulusLength).ToArray() : Example1.Modulus!;

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => key.ApplyPrivateExponent(message, new byte[key.ModulusLength]));

        Assert.AreEqual("message", exception.ParamName);
    }

    [TestMethod]
    [DataRow(127, 136, 128, "message")]
    [DataRow(128, 135, 128, "blindingRandom")]
    [DataRow(128, 136, 129, "destination")]
    public void ApplyPrivateExponent_WrongLength_ThrowsArgumentException(int messageLength, int randomLength, int destinationLength, string parameter)
    {
        using var key = new RsaCrtPrivateKey(Example1);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => key.ApplyPrivateExponent(new byte[messageLength], new byte[randomLength], new byte[destinationLength]));

        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    public void ApplyPrivateExponent_InconsistentCrtExponent_ThrowsCryptographicExceptionAndWritesNothing()
    {
        RSAParameters broken = Example1;
        broken.DP = (byte[])Example1.DP!.Clone();
        broken.DP[^1] ^= 0x02;
        using var key = new RsaCrtPrivateKey(broken);
        byte[] signature = new byte[key.ModulusLength];

        Assert.ThrowsExactly<CryptographicException>(
            () => key.ApplyPrivateExponent(ApplyPublicExponent(Example1, Convert.FromHexString(Example1Signature)), signature));

        Assert.IsTrue(signature.All(value => value == 0));
    }

    [TestMethod]
    public void Constructor_MissingOrEmptyCrtValue_ThrowsArgumentException()
    {
        RSAParameters withoutP = Example1;
        withoutP.P = null;
        RSAParameters emptyQ = Example1;
        emptyQ.Q = [];

        Assert.ThrowsExactly<ArgumentException>(() => new RsaCrtPrivateKey(withoutP));
        Assert.ThrowsExactly<ArgumentException>(() => new RsaCrtPrivateKey(emptyQ));
    }

    [TestMethod]
    public void Dispose_ThenApplyPrivateExponent_ThrowsObjectDisposedException()
    {
        var key = new RsaCrtPrivateKey(Example1);

        key.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => key.ApplyPrivateExponent(new byte[128], new byte[128]));
    }

    private static byte[] ApplyPublicExponent(RSAParameters parameters, byte[] signature)
    {
        BigInteger value = BigInteger.ModPow(ToInteger(signature), ToInteger(parameters.Exponent!), ToInteger(parameters.Modulus!));
        byte[] bytes = new byte[parameters.Modulus!.Length];
        value.TryWriteBytes(bytes.AsSpan(bytes.Length - value.GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);
        return bytes;
    }

    private static BigInteger ToInteger(ReadOnlySpan<byte> bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);
}
