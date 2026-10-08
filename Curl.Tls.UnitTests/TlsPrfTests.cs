using System.Security.Cryptography;
using System.Text;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The TLS PRF against outputs recorded from OpenSSL 3.5.7's <c>TLS1-PRF</c> KDF
/// (<c>openssl kdf -keylen N -kdfopt digest:D -kdfopt hexsecret:S -kdfopt hexseed:L+SEED TLS1-PRF</c>,
/// run 2026-09-28), and the derived secrets against <see cref="ReferencePrf" />, an
/// independent transcription of RFC 5246 section 5 and RFC 2246 section 5.
/// </summary>
[TestClass]
public sealed class TlsPrfTests
{
    private static readonly byte[] TestLabelSeed = Convert.FromHexString("a0ba9f936cda311827a6f796ffd5198c");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Sha256MatchesOpenSsl()
    {
        // digest:SHA256, secret 9bbe436ba940f017b17652849a71db35, label "test label"; the IETF TLS list's P_SHA256 vector.
        byte[] expected = Convert.FromHexString(
            "E3F229BA727BE17B8D122620557CD453C2AAB21D07C3D495329B52D4E61EDB5A6B301791E90D35C9C9A46B4E14BAF9AF0FA022F7077DEF17ABFD3797C0564BAB"
            + "4FBC91666E9DEF9B97FCE34F796789BAA48082D122EE42C5A72E5A5110FFF70187347B66");
        Diagnostics.Arrange("prf", "SHA-256, secret 9bbe436ba940f017b17652849a71db35, label test label, 100 bytes");
        Diagnostics.Bytes("seed", TestLabelSeed);

        byte[] actual = TlsPrf.Sha256.Compute(Convert.FromHexString("9bbe436ba940f017b17652849a71db35"), "test label", TestLabelSeed, 100);
        Diagnostics.Act("output length", actual.Length);

        Diagnostics.Diff("prf output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Sha384MatchesOpenSsl()
    {
        // digest:SHA384, secret b80b733d6ceefcdc71566ea48e5567df, seed cd665cf6a8447dd6ff8b27555edb7465, 148 bytes.
        byte[] expected = Convert.FromHexString(
            "7B0C18E9CED410ED1804F2CFA34A336A1C14DFFB4900BB5FD7942107E81C83CDE9CA0FAA60BE9FE34F82B1233C9146A0E534CB400FED2700884F9DC236F80EDD"
            + "8BFA961144C9E8D792ECA722A7B32FC3D416D473EBC2C5FD4ABFDAD05D9184259B5BF8CD4D90FA0D31E2DEC479E4F1A26066F2EEA9A69236A3E52655C9E9AEE6"
            + "91C8F3A26854308D5EAA3BE85E0990703D73E56F");
        Diagnostics.Arrange("prf", "SHA-384, secret b80b733d6ceefcdc71566ea48e5567df, label test label, seed cd665cf6a8447dd6ff8b27555edb7465, 148 bytes");

        byte[] actual = TlsPrf.Sha384.Compute(
            Convert.FromHexString("b80b733d6ceefcdc71566ea48e5567df"),
            "test label",
            Convert.FromHexString("cd665cf6a8447dd6ff8b27555edb7465"),
            148);
        Diagnostics.Act("output length", actual.Length);

        Diagnostics.Diff("prf output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Md5Sha1MatchesOpenSslWithAnEvenSecret()
    {
        // digest:MD5-SHA1, the 16-byte secret split into two 8-byte halves.
        byte[] expected = Convert.FromHexString("661740E6F98BC901EFD2738502A71C03F76DD2F86298549B1148EFF06714CF0F6B7C532CD8C69F1530E0BB680EEC34C4");
        Diagnostics.Arrange("prf", "MD5-SHA1, 16-byte secret 9bbe436ba940f017b17652849a71db35, label test label, 48 bytes");
        Diagnostics.Bytes("seed", TestLabelSeed);

        byte[] actual = TlsPrf.Md5Sha1.Compute(Convert.FromHexString("9bbe436ba940f017b17652849a71db35"), "test label", TestLabelSeed, 48);
        Diagnostics.Act("output length", actual.Length);

        Diagnostics.Diff("prf output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Md5Sha1MatchesOpenSslWithAnOddSecretWhoseHalvesShareTheMiddleByte()
    {
        // digest:MD5-SHA1, secret 000102...0c: 13 bytes, halves of 7 overlapping at 06 (RFC 2246 section 5).
        byte[] expected = Convert.FromHexString(
            "B6E2F064AE4C104A8B2892BC54FF07B6E2142B7D0BA49385DE7290D1BD62DEE9A1A3B662F49E237EFA60CD1FC9E90AEEB7A1411DA97A74107679A55F263248D9"
            + "1AE3E844B96865B23C399234304B5DFC020B39E544B4EF5E6F2D708595076FD414E293DA86D9055A");
        Diagnostics.Arrange("prf", "MD5-SHA1, 13-byte secret 000102030405060708090a0b0c, label test label, 104 bytes");
        Diagnostics.Bytes("seed", TestLabelSeed);

        byte[] actual = TlsPrf.Md5Sha1.Compute(Convert.FromHexString("000102030405060708090a0b0c"), "test label", TestLabelSeed, 104);
        Diagnostics.Act("output length", actual.Length);

        Diagnostics.Diff("prf output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ComputeWithLengthZeroReturnsNothing()
    {
        Diagnostics.Arrange("prf", "SHA-256, secret 010203, label x, empty seed, length 0");

        byte[] actual = TlsPrf.Sha256.Compute([1, 2, 3], "x", [], 0);
        Diagnostics.Act("output length", actual.Length);

        Diagnostics.Assert("output length", 0, actual.Length);
        Assert.IsEmpty(actual);
    }

    [TestMethod]
    public void ComputeRefusesANegativeLength()
    {
        Diagnostics.Arrange("prf", "SHA-256, secret 01, label x, empty seed, length -1");

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TlsPrf.Sha256.Compute([1], "x", [], -1));
        Diagnostics.Act("exception parameter", exception.ParamName);

        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void MasterSecretSeedsWithTheClientRandomThenTheServerRandom()
    {
        byte[] preMaster = Filled(48, 0x11);
        byte[] clientRandom = Filled(32, 0x22);
        byte[] serverRandom = Filled(32, 0x33);
        Diagnostics.Arrange("inputs", "48-byte pre-master 0x11, client random 32 x 0x22, server random 32 x 0x33");

        byte[] sha256Master = TlsPrf.Sha256.ComputeMasterSecret(preMaster, clientRandom, serverRandom);
        byte[] md5Sha1Master = TlsPrf.Md5Sha1.ComputeMasterSecret(preMaster, clientRandom, serverRandom);
        Diagnostics.Act("master secret lengths", $"{sha256Master.Length}, {md5Sha1Master.Length}");

        byte[] sha256Expected = ReferencePrf.Tls12(HashAlgorithmName.SHA256, preMaster, "master secret", [.. clientRandom, .. serverRandom], 48);
        byte[] md5Sha1Expected = ReferencePrf.Tls10(preMaster, "master secret", [.. clientRandom, .. serverRandom], 48);
        Diagnostics.Diff("SHA-256 master secret", sha256Expected, sha256Master);
        Diagnostics.Diff("MD5-SHA1 master secret", md5Sha1Expected, md5Sha1Master);
        CollectionAssert.AreEqual(sha256Expected, sha256Master);
        CollectionAssert.AreEqual(md5Sha1Expected, md5Sha1Master);
    }

    [TestMethod]
    public void ExtendedMasterSecretSeedsWithTheSessionHash()
    {
        byte[] preMaster = Filled(32, 0x44);
        byte[] sessionHash = SHA384.HashData(Encoding.ASCII.GetBytes("handshake up to ClientKeyExchange"));
        Diagnostics.Arrange("pre-master", "32 x 0x44");
        Diagnostics.Bytes("session hash", sessionHash);

        byte[] actual = TlsPrf.Sha384.ComputeExtendedMasterSecret(preMaster, sessionHash);
        Diagnostics.Act("extended master secret length", actual.Length);

        byte[] expected = ReferencePrf.Tls12(HashAlgorithmName.SHA384, preMaster, "extended master secret", sessionHash, 48);
        Diagnostics.Diff("extended master secret", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void KeyBlockSeedsWithTheServerRandomThenTheClientRandom()
    {
        byte[] master = Filled(48, 0x55);
        byte[] clientRandom = Filled(32, 0x22);
        byte[] serverRandom = Filled(32, 0x33);
        Diagnostics.Arrange("inputs", "MD5-SHA1, master 48 x 0x55, client random 32 x 0x22, server random 32 x 0x33, 104 bytes");

        byte[] actual = TlsPrf.Md5Sha1.ComputeKeyBlock(master, serverRandom, clientRandom, 104);
        Diagnostics.Act("key block length", actual.Length);

        byte[] expected = ReferencePrf.Tls10(master, "key expansion", [.. serverRandom, .. clientRandom], 104);
        Diagnostics.Diff("key block", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void VerifyDataIsTwelveBytesUnderEachSidesLabel()
    {
        byte[] master = Filled(48, 0x66);
        byte[] handshakeHash = SHA256.HashData([1, 2, 3]);
        Diagnostics.Arrange("master", "48 x 0x66");
        Diagnostics.Bytes("handshake hash", handshakeHash);

        byte[] client = TlsPrf.Sha256.ComputeClientVerifyData(master, handshakeHash);
        byte[] server = TlsPrf.Sha256.ComputeServerVerifyData(master, handshakeHash);
        Diagnostics.Act("verify data lengths", $"{client.Length}, {server.Length}");

        byte[] clientExpected = ReferencePrf.Tls12(HashAlgorithmName.SHA256, master, "client finished", handshakeHash, 12);
        byte[] serverExpected = ReferencePrf.Tls12(HashAlgorithmName.SHA256, master, "server finished", handshakeHash, 12);
        Diagnostics.Diff("client verify data", clientExpected, client);
        Diagnostics.Diff("server verify data", serverExpected, server);
        CollectionAssert.AreEqual(clientExpected, client);
        CollectionAssert.AreEqual(serverExpected, server);
    }

    private static byte[] Filled(int length, byte value) => Enumerable.Repeat(value, length).ToArray();

    /// <summary>RFC 5246 section 5 and RFC 2246 section 5, written out with the HMAC classes.</summary>
    private static class ReferencePrf
    {
        public static byte[] Tls12(HashAlgorithmName hash, byte[] secret, string label, byte[] seed, int length) =>
            PHash(CreateHmac(hash, secret), [.. Encoding.ASCII.GetBytes(label), .. seed], length);

        public static byte[] Tls10(byte[] secret, string label, byte[] seed, int length)
        {
            int half = (secret.Length + 1) / 2;
            byte[] labelAndSeed = [.. Encoding.ASCII.GetBytes(label), .. seed];
            byte[] md5 = PHash(new HMACMD5(secret[..half]), labelAndSeed, length);
            byte[] sha1 = PHash(new HMACSHA1(secret[^half..]), labelAndSeed, length);
            return md5.Zip(sha1, (left, right) => (byte)(left ^ right)).ToArray();
        }

        private static HMAC CreateHmac(HashAlgorithmName hash, byte[] key) =>
            hash == HashAlgorithmName.SHA384 ? new HMACSHA384(key) : new HMACSHA256(key);

        private static byte[] PHash(HMAC hmac, byte[] seed, int length)
        {
            using (hmac)
            {
                List<byte> output = [];
                byte[] a = seed;
                while (output.Count < length)
                {
                    a = hmac.ComputeHash(a);
                    output.AddRange(hmac.ComputeHash([.. a, .. seed]));
                }

                return output.Take(length).ToArray();
            }
        }
    }
}
