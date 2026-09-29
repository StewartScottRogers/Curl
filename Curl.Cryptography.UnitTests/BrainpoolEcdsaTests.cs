using System.Security.Cryptography;
using System.Text;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="BrainpoolEcdsa" /> to every vector of Wycheproof's IEEE P1363 brainpool
/// ECDSA files for the three TLS 1.3 schemes (RFC 8734): brainpoolP256r1 with SHA-256,
/// P384r1 with SHA-384 and P512r1 with SHA-512, valid and invalid, copied from
/// C2SP/wycheproof <c>testvectors_v1</c> into <c>KnownAnswers/wycheproof-ecdsa-*-p1363.txt</c>
/// (one line per test: tcId, result, message, r || s). Signing is checked by round trip,
/// determinism and, on Windows, by CNG's own brainpool verification.
/// </summary>
[TestClass]
public sealed class BrainpoolEcdsaTests
{
    // RFC 7027 A.1 to A.3's dA and its public key: a known key on each curve.
    private static readonly string[] PrivateKeys =
    [
        "81DB1EE100150FF2EA338D708271BE38300CB54241D79950F77B063039804F1D",
        "1E20F5E048A5886F1F157C74E91BDE2B98C8B52D58E5003D57053FC4B0BD65D6F15EB5D1EE1610DF870795143627D042",
        "16302FF0DBBB5A8D733DAB7141C1B45ACBC8715939677F6A56850A38BD87BD59B09E80279609FF333EB9D4C061231FB26F92EEB04982A5F1D1764CAD57665422",
    ];

    private static readonly string[] PublicKeys =
    [
        "04"
        + "44106E913F92BC02A1705D9953A8414DB95E1AAA49E81D9E85F929A8E3100BE5"
        + "8AB4846F11CACCB73CE49CBDD120F5A900A69FD32C272223F789EF10EB089BDC",
        "04"
        + "68B665DD91C195800650CDD363C625F4E742E8134667B767B1B476793588F885AB698C852D4A6E77A252D6380FCAF068"
        + "55BC91A39C9EC01DEE36017B7D673A931236D2F1F5C83942D049E3FA20607493E0D038FF2FD30C2AB67D15C85F7FAA59",
        "04"
        + "0A420517E406AAC0ACDCE90FCD71487718D3B953EFD7FBEC5F7F27E28C6149999397E91E029E06457DB2D3E640668B392C2A7E737A7F0BF04436D11640FD09FD"
        + "72E6882E8DB28AAD36237CD25D580DB23783961C8DC52DFA2EC138AD472A0FCEF3887CF62B623B2A87DE5C588301EA3E5FC269B373B60724F5E82A6AD147FDE7",
    ];

    // RFC 5639 section 3: the generators, as uncompressed points, and the group orders q.
    private static readonly string[] Generators =
    [
        "04"
        + "8BD2AEB9CB7E57CB2C4B482FFC81B7AFB9DE27E1E3BD23C23A4453BD9ACE3262"
        + "547EF835C3DAC4FD97F8461A14611DC9C27745132DED8E545C1D54C72F046997",
        "04"
        + "1D1C64F068CF45FFA2A63A81B7C13F6B8847A3E77EF14FE3DB7FCAFE0CBD10E8E826E03436D646AAEF87B2E247D4AF1E"
        + "8ABE1D7520F9C2A45CB1EB8E95CFD55262B70B29FEEC5864E19C054FF99129280E4646217791811142820341263C5315",
        "04"
        + "81AEE4BDD82ED9645A21322E9C4C6A9385ED9F70B5D916C1B43B62EEF4D0098EFF3B1F78E2D0D48D50D1687B93B97D5F7C6D5047406A5E688B352209BCB9F822"
        + "7DDE385D566332ECC0EABFA9CF7822FDF209F70024A57B1AA000C55B881F8111B2DCDE494A5F485E5BCA4BD88A2763AED1CA2B2FA8F0540678CD1E0F3AD80892",
    ];

    private static readonly string[] Orders =
    [
        "A9FB57DBA1EEA9BC3E660A909D838D718C397AA3B561A6F7901E0E82974856A7",
        "8CB91E82A3386D280F5D6F7E50E641DF152F7109ED5456B31F166E6CAC0425A7CF3AB6AF6B7FC3103B883202E9046565",
        "AADD9DB8DBE9C48B3FD4E6AE33C9FC07CB308DB3B3C9D20ED6639CCA70330870553E5C414CA92619418661197FAC10471DB1D381085DDADDB58796829CA90069",
    ];

    [TestMethod]
    public void VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult()
    {
        AssertEveryWycheproofVector(BrainpoolCurve.BrainpoolP256r1, "wycheproof-ecdsa-brainpoolP256r1-sha256-p1363.txt", "SHA256", 261);
    }

    // Several seconds each in a Debug build, so outside the fast run (ADR-0118, "Tests").
    [TestMethod]
    [TestCategory("Integration")]
    [DataRow(BrainpoolCurve.BrainpoolP384r1, "wycheproof-ecdsa-brainpoolP384r1-sha384-p1363.txt", "SHA384", 292)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1, "wycheproof-ecdsa-brainpoolP512r1-sha512-p1363.txt", "SHA512", 337)]
    public void VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult(BrainpoolCurve curve, string resourceName, string hashName, int testCount)
    {
        AssertEveryWycheproofVector(curve, resourceName, hashName, testCount);
    }

    private static void AssertEveryWycheproofVector(BrainpoolCurve curve, string resourceName, string hashName, int testCount)
    {
        List<string> failures = [];
        int tests = 0;
        byte[] publicKey = [];
        foreach (string line in ReadLines(resourceName))
        {
            string[] fields = line.Split(' ');
            if (fields[0] == "key")
            {
                publicKey = Convert.FromHexString(fields[1]);
                continue;
            }

            tests++;
            byte[] hash = CryptographicOperations.HashData(new HashAlgorithmName(hashName), FromHexOrEmpty(fields[2]));
            bool verified = BrainpoolEcdsa.VerifyHash(curve, publicKey, hash, FromHexOrEmpty(fields[3]));
            if (verified != (fields[1] == "valid"))
            {
                failures.Add(fields[0]);
            }
        }

        Assert.AreEqual(testCount, tests);
        Assert.AreEqual(string.Empty, string.Join(", ", failures), "tcIds with the wrong result");
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1, "SHA1")]
    [DataRow(BrainpoolCurve.BrainpoolP256r1, "SHA256")]
    [DataRow(BrainpoolCurve.BrainpoolP256r1, "SHA512")]
    [DataRow(BrainpoolCurve.BrainpoolP384r1, "SHA224")]
    [DataRow(BrainpoolCurve.BrainpoolP384r1, "SHA384")]
    [DataRow(BrainpoolCurve.BrainpoolP512r1, "SHA256")]
    [DataRow(BrainpoolCurve.BrainpoolP512r1, "SHA512")]
    public void SignHash_ThenVerifyHash_AcceptsTheSignatureAndRejectsAnyOtherMessage(BrainpoolCurve curve, string hashName)
    {
        HashAlgorithmName hashAlgorithm = new(hashName);
        using BrainpoolEcdsa key = new(curve, Convert.FromHexString(PrivateKeys[(int)curve]));
        byte[] publicKey = Convert.FromHexString(PublicKeys[(int)curve]);
        byte[] hash = DsaSignature.HashData(Encoding.ASCII.GetBytes("sample"), hashAlgorithm);
        byte[] otherHash = DsaSignature.HashData(Encoding.ASCII.GetBytes("test"), hashAlgorithm);
        byte[] signature = new byte[key.SignatureLength];
        byte[] again = new byte[key.SignatureLength];

        key.SignHash(hash, hashAlgorithm, signature);
        key.SignHash(hash, hashAlgorithm, again);

        CollectionAssert.AreEqual(signature, again);
        Assert.IsTrue(BrainpoolEcdsa.VerifyHash(curve, publicKey, hash, signature));
        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(curve, publicKey, otherHash, signature));
    }

    [TestMethod]
    public void SignHash_ManyMessages_EachVerifies()
    {
        using BrainpoolEcdsa key = new(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(PrivateKeys[0]));
        byte[] publicKey = Convert.FromHexString(PublicKeys[0]);
        byte[] signature = new byte[64];
        for (int message = 0; message < 8; message++)
        {
            byte[] hash = SHA256.HashData([(byte)message]);

            key.SignHash(hash, HashAlgorithmName.SHA256, signature);

            Assert.IsTrue(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, publicKey, hash, signature), $"message {message}");
        }
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1)]
    public void ExportPublicKey_Rfc7027Key_GivesItsPublicKey(BrainpoolCurve curve)
    {
        using BrainpoolEcdsa key = new(curve, Convert.FromHexString(PrivateKeys[(int)curve]));
        byte[] publicKey = new byte[BrainpoolEcdh.GetPublicKeyLength(curve)];

        key.ExportPublicKey(publicKey);

        Assert.AreEqual(PublicKeys[(int)curve], Convert.ToHexString(publicKey));
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1, 64)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1, 96)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1, 128)]
    public void SignatureLength_EachCurve_IsTwiceTheOrderLength(BrainpoolCurve curve, int length)
    {
        using BrainpoolEcdsa key = new(curve, Convert.FromHexString(PrivateKeys[(int)curve]));

        Assert.AreEqual(length, key.SignatureLength);
        Assert.AreEqual(length, BrainpoolEcdsa.GetSignatureLength(curve));
    }

    [TestMethod]
    public void VerifyHash_SumAtInfinity_ReturnsFalse()
    {
        // With Q = G (d = 1), r = 1, s = 1 and z = q - 1, R = (z + r) * G is the point at infinity.
        byte[] hash = Convert.FromHexString(Orders[0]);
        hash[^1] -= 1;
        byte[] signature = new byte[64];
        signature[31] = 1;
        signature[63] = 1;

        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(Generators[0]), hash, signature));
    }

    [TestMethod]
    public void VerifyHash_MalformedInputs_ReturnFalse()
    {
        using BrainpoolEcdsa key = new(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(PrivateKeys[0]));
        byte[] publicKey = Convert.FromHexString(PublicKeys[0]);
        byte[] hash = SHA256.HashData("sample"u8);
        byte[] signature = new byte[64];
        key.SignHash(hash, HashAlgorithmName.SHA256, signature);
        byte[] zeroR = (byte[])signature.Clone();
        Array.Clear(zeroR, 0, 32);
        byte[] sAtOrder = (byte[])signature.Clone();
        Convert.FromHexString(Orders[0]).CopyTo(sAtOrder, 32);
        byte[] offCurve = (byte[])publicKey.Clone();
        offCurve[^1] ^= 1;

        Assert.IsTrue(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, publicKey, hash, signature));
        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, publicKey, hash, signature[..^1]));
        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, publicKey, hash, zeroR));
        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, publicKey, hash, sAtOrder));
        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, offCurve, hash, signature));
        Assert.IsFalse(BrainpoolEcdsa.VerifyHash(BrainpoolCurve.BrainpoolP256r1, [0x00], hash, signature));
    }

    [TestMethod]
    public void Constructor_PrivateKeyOutOfRange_ThrowsArgument()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new BrainpoolEcdsa(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(Orders[0])));
    }

    [TestMethod]
    public void Constructor_UnknownCurve_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BrainpoolEcdsa((BrainpoolCurve)(-1), new byte[32]));
    }

    [TestMethod]
    public void SignHash_WrongLengthsOrHash_ThrowArgument()
    {
        using BrainpoolEcdsa key = new(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(PrivateKeys[0]));

        Assert.ThrowsExactly<ArgumentException>(() => key.SignHash(new byte[31], HashAlgorithmName.SHA256, new byte[64]));
        Assert.ThrowsExactly<ArgumentException>(() => key.SignHash(new byte[32], HashAlgorithmName.SHA256, new byte[63]));
        Assert.ThrowsExactly<ArgumentException>(() => key.SignHash(new byte[16], HashAlgorithmName.MD5, new byte[64]));
        Assert.ThrowsExactly<ArgumentException>(() => key.ExportPublicKey(new byte[64]));
    }

    [TestMethod]
    public void Dispose_ThenUse_ThrowsObjectDisposed()
    {
        BrainpoolEcdsa key = new(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(PrivateKeys[0]));

        key.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => key.SignHash(new byte[32], HashAlgorithmName.SHA256, new byte[64]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportPublicKey(new byte[65]));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(BrainpoolCurve.BrainpoolP256r1, "SHA256")]
    [DataRow(BrainpoolCurve.BrainpoolP384r1, "SHA384")]
    [DataRow(BrainpoolCurve.BrainpoolP512r1, "SHA512")]
    public void SignHash_VerifiedByWindowsCng_IsValid(BrainpoolCurve curve, string hashName)
    {
        HashAlgorithmName hashAlgorithm = new(hashName);
        byte[] publicKey = Convert.FromHexString(PublicKeys[(int)curve]);
        int length = (publicKey.Length - 1) / 2;
        ECCurve namedCurve = curve switch
        {
            BrainpoolCurve.BrainpoolP256r1 => ECCurve.NamedCurves.brainpoolP256r1,
            BrainpoolCurve.BrainpoolP384r1 => ECCurve.NamedCurves.brainpoolP384r1,
            _ => ECCurve.NamedCurves.brainpoolP512r1,
        };
        using ECDsa cng = ECDsa.Create(new ECParameters
        {
            Curve = namedCurve,
            Q = new ECPoint { X = publicKey[1..(1 + length)], Y = publicKey[(1 + length)..] },
        });
        using BrainpoolEcdsa key = new(curve, Convert.FromHexString(PrivateKeys[(int)curve]));
        byte[] hash = CryptographicOperations.HashData(hashAlgorithm, "sample"u8);
        byte[] signature = new byte[key.SignatureLength];

        key.SignHash(hash, hashAlgorithm, signature);

        Assert.IsTrue(cng.VerifyHash(hash, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    private static byte[] FromHexOrEmpty(string hex) => hex == "-" ? [] : Convert.FromHexString(hex);

    private static IEnumerable<string> ReadLines(string resourceName)
    {
        using Stream stream = typeof(BrainpoolEcdsaTests).Assembly.GetManifestResourceStream("KnownAnswers." + resourceName)!;
        using StreamReader reader = new(stream);
        while (reader.ReadLine() is string line)
        {
            if (!line.StartsWith('#'))
            {
                yield return line;
            }
        }
    }
}
