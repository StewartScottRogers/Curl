using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Tls;

/// <summary>
/// Reproduces every value of RFC 5054 Appendix B (the SRP test vectors) with
/// <see cref="SrpClient" />, and pins <see cref="SrpGroup" />'s table to Appendix A.
/// </summary>
[TestClass]
public sealed class SrpClientTests
{
    private static readonly byte[] Identity = Encoding.UTF8.GetBytes("alice");
    private static readonly byte[] Password = Encoding.UTF8.GetBytes("password123");
    private static readonly byte[] Salt = Hex("BEB25379 D1A8581E B5A72767 3A2441EE");

    private static readonly byte[] PrivateValueA = Hex("60975527 035CF2AD 1989806F 0407210B C81EDC04 E2762A56 AFD529DD DA2D4393");

    private static readonly byte[] PrivateValueB = Hex("E487CB59 D31AC550 471E81F0 0F6928E0 1DDA08E9 74A004F4 9E61F5D1 05284D20");

    private static readonly byte[] ExpectedVerifier = Hex(
        "7E273DE8 696FFC4F 4E337D05 B4B375BE B0DDE156 9E8FA00A 9886D812 9BADA1F1 822223CA 1A605B53 0E379BA4 729FDC59 F105B478 7E5186F5 " +
        "C671085A 1447B52A 48CF1970 B4FB6F84 00BBF4CE BFBB1681 52E08AB5 EA53D15C 1AFF87B2 B9DA6E04 E058AD51 CC72BFC9 033B564E 26480D78 " +
        "E955A5E2 9E7AB245 DB2BE315 E2099AFB");

    private static readonly byte[] ExpectedA = Hex(
        "61D5E490 F6F1B795 47B0704C 436F523D D0E560F0 C64115BB 72557EC4 4352E890 3211C046 92272D8B 2D1A5358 A2CF1B6E 0BFCF99F 921530EC " +
        "8E393561 79EAE45E 42BA92AE ACED8251 71E1E8B9 AF6D9C03 E1327F44 BE087EF0 6530E69F 66615261 EEF54073 CA11CF58 58F0EDFD FE15EFEA " +
        "B349EF5D 76988A36 72FAC47B 0769447B");

    private static readonly byte[] ExpectedB = Hex(
        "BD0C6151 2C692C0C B6D041FA 01BB152D 4916A1E7 7AF46AE1 05393011 BAF38964 DC46A067 0DD125B9 5A981652 236F99D9 B681CBF8 7837EC99 " +
        "6C6DA044 53728610 D0C6DDB5 8B318885 D7D82C7F 8DEB75CE 7BD4FBAA 37089E6F 9C6059F3 88838E7A 00030B33 1EB76840 910440B1 B27AAEAE " +
        "EB4012B7 D7665238 A8E3FB00 4B117B58");

    private static readonly byte[] ExpectedPremasterSecret = Hex(
        "B0DC82BA BCF30674 AE450C02 87745E79 90A3381F 63B387AA F271A10D 233861E3 59B48220 F7C4693C 9AE12B0A 6F67809F 0876E2D0 13800D6C " +
        "41BB59B6 D5979B5C 00A172B4 A2A5903A 0BDCAF8A 709585EB 2AFAFA8F 3499B200 210DCC1F 10EB3394 3CD67FC8 8A2F39A4 BE5BEC4E C0A3212D " +
        "C346D7E4 74B29EDE 8A469FFE CA686E5A");

    [TestMethod]
    public void MultiplierIsAppendixBsK() =>
        CollectionAssert.AreEqual(Hex("7556AA04 5AEF2CDD 07ABAF0F 665C3E81 8913186F"), SrpClient.ComputeMultiplier(SrpGroup.Bits1024));

    [TestMethod]
    public void PrivateKeyIsAppendixBsX() =>
        CollectionAssert.AreEqual(Hex("94B7555A ABE9127C C58CCF49 93DB6CF8 4D16C124"), SrpClient.ComputePrivateKey(Salt, Identity, Password));

    [TestMethod]
    public void VerifierIsAppendixBsV() =>
        CollectionAssert.AreEqual(ExpectedVerifier, SrpClient.ComputeVerifier(SrpGroup.Bits1024, SrpClient.ComputePrivateKey(Salt, Identity, Password)));

    [TestMethod]
    public void ClientPublicValueIsAppendixBsA() =>
        CollectionAssert.AreEqual(ExpectedA, SrpClient.ComputePublicValue(SrpGroup.Bits1024, PrivateValueA));

    [TestMethod]
    public void ServerPublicValueFromTheVerifierIsAppendixBsB()
    {
        BigInteger n = Integer(SrpGroup.Bits1024.Prime);
        BigInteger k = Integer(SrpClient.ComputeMultiplier(SrpGroup.Bits1024));
        BigInteger b = ((k * Integer(ExpectedVerifier)) + BigInteger.ModPow(2, Integer(PrivateValueB), n)) % n;

        CollectionAssert.AreEqual(ExpectedB, b.ToByteArray(isUnsigned: true, isBigEndian: true));
    }

    [TestMethod]
    public void ScramblerIsAppendixBsU() =>
        CollectionAssert.AreEqual(Hex("CE38B959 3487DA98 554ED47D 70A7AE5F 462EF019"), SrpClient.ComputeScrambler(SrpGroup.Bits1024, ExpectedA, ExpectedB));

    [TestMethod]
    public void PremasterSecretIsAppendixBs()
    {
        byte[] privateKey = SrpClient.ComputePrivateKey(Salt, Identity, Password);

        CollectionAssert.AreEqual(ExpectedPremasterSecret, SrpClient.ComputePremasterSecret(SrpGroup.Bits1024, privateKey, PrivateValueA, ExpectedB));
    }

    [TestMethod]
    public void ServerComputesTheSamePremasterSecretFromAAndTheVerifier()
    {
        BigInteger n = Integer(SrpGroup.Bits1024.Prime);
        BigInteger u = Integer(SrpClient.ComputeScrambler(SrpGroup.Bits1024, ExpectedA, ExpectedB));
        BigInteger basis = Integer(ExpectedA) * BigInteger.ModPow(Integer(ExpectedVerifier), u, n) % n;

        CollectionAssert.AreEqual(ExpectedPremasterSecret, BigInteger.ModPow(basis, Integer(PrivateValueB), n).ToByteArray(isUnsigned: true, isBigEndian: true));
    }

    [TestMethod]
    public void ScramblerPadsShortAndLeadingZeroValuesToTheLengthOfN()
    {
        byte[] padded = new byte[SrpGroup.Bits1024.PrimeLength];
        padded[^1] = 7;

        byte[] expected = SHA1.HashData([.. padded, .. padded]);

        CollectionAssert.AreEqual(expected, SrpClient.ComputeScrambler(SrpGroup.Bits1024, [7], [0, 0, 7]));
    }

    // SHA-256 of each prime's upper-case hex, computed from the text of RFC 5054 Appendix A.
    [TestMethod]
    [DataRow(0, 1024, 2, "1a3e3c44dbd89f1f9840f1dd76f580309192829edf55b0c518833a3414f4f20e")]
    [DataRow(1, 1536, 2, "843dbf0c4eed0ff9db74d30190c57463e5a2295605bc6f9c3893277680b63664")]
    [DataRow(2, 2048, 2, "a5f6ed3cab1c9d28a1e9a3697cf264210e2ace36b90395860a6fcbd06bb7babc")]
    [DataRow(3, 3072, 5, "2b2c15e1523695d748a2f56b3b81d59d23467b300f1879585be61f0addacdc45")]
    [DataRow(4, 4096, 5, "2349a6f8251156cbfade78ada6bd1e0c961a56847def4d288b6fdbd740016bff")]
    [DataRow(5, 6144, 5, "eeff9f22efbb6af437e9bb4a5466ddc607a9d0c954e14a4a90461688896b9e2b")]
    [DataRow(6, 8192, 19, "d0c08f76798cac51345c195a01a2bea21bc577cfa9d857bbc6627ee41cb0b2b6")]
    public void GroupTableIsAppendixA(int index, int bits, int generator, string primeHexSha256)
    {
        SrpGroup group = SrpGroup.All[index];

        Assert.AreEqual(bits / 8, group.PrimeLength);
        Assert.AreEqual(bits, (int)Integer(group.Prime).GetBitLength());
        CollectionAssert.AreEqual(new[] { (byte)generator }, group.Generator.ToArray());
        Assert.AreEqual(primeHexSha256, Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(Convert.ToHexString(group.Prime)))));
        Assert.AreSame(group, SrpGroup.Find(group.Prime, group.Generator));
    }

    [TestMethod]
    public void FindIgnoresLeadingZeroBytes() =>
        Assert.AreSame(SrpGroup.Bits2048, SrpGroup.Find([0, .. SrpGroup.Bits2048.Prime], [0, 2]));

    [TestMethod]
    public void FindRefusesAKnownPrimeWithAnotherGenerator() =>
        Assert.IsNull(SrpGroup.Find(SrpGroup.Bits1024.Prime, [5]));

    [TestMethod]
    public void FindRefusesAPrimeOutsideAppendixA() =>
        Assert.IsNull(SrpGroup.Find(FiniteFieldDhGroupPrime(), [2]));

    [TestMethod]
    public void EveryMethodRefusesANullGroup()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpClient.ComputeMultiplier(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpClient.ComputeVerifier(null!, [1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpClient.ComputePublicValue(null!, [1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpClient.ComputeScrambler(null!, [1], [1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpClient.ComputePremasterSecret(null!, [1], [1], [1]));
    }

    private static byte[] FiniteFieldDhGroupPrime() => Curl.Cryptography.FiniteFieldDiffieHellmanGroup.Ffdhe2048.Prime.ToArray();

    private static BigInteger Integer(ReadOnlySpan<byte> value) => new(value, isUnsigned: true, isBigEndian: true);

    private static byte[] Hex(string spaced) => Convert.FromHexString(spaced.Replace(" ", string.Empty, StringComparison.Ordinal));
}
