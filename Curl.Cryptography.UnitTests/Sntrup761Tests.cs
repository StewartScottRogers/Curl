using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Sntrup761" /> to the NTRU Prime round-3 submission's known answers and to
/// implicit rejection of a tampered ciphertext (ADR-0118).
/// </summary>
[TestClass]
public sealed class Sntrup761Tests
{
    private const int P = 761;

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // The first three vectors of KAT/kem/sntrup761/kat_kem.rsp in "NTRU Prime: round 3"
    // (ntruprime-20201007.tar.gz, https://ntruprime.cr.yp.to/nist/ntruprime-20201007.tar.gz),
    // copied unchanged into KnownAnswers/sntrup761-kat_kem-first-three.txt (.rsp is gitignored). Each vector's
    // randomness comes from NIST's AES-256 CTR_DRBG seeded with its "seed" line, drawn in the
    // reference's pieces: key generation takes 761 four-byte calls for g, 761 for f and one
    // 191-byte call for rho; encapsulation takes 761 four-byte calls for r.
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void KeyGenerationEncapsulationAndDecapsulation_Round3KnownAnswer_MatchByteForByte(int count)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyDictionary<string, string> vector = ReadKnownAnswer(count);
        using NistKnownAnswerRandom drbg = new(Convert.FromHexString(vector["seed"]));
        byte[] keyRandom = [.. drbg.NextBytesInCalls(2 * P, 4), .. drbg.NextBytesInCalls(1, 191)];
        byte[] encapsulationRandom = drbg.NextBytesInCalls(P, 4);
        byte[] publicKey = new byte[Sntrup761.PublicKeySize];
        byte[] secretKey = new byte[Sntrup761.SecretKeySize];
        byte[] ciphertext = new byte[Sntrup761.CiphertextSize];
        byte[] sharedSecret = new byte[Sntrup761.SharedSecretSize];
        byte[] decapsulated = new byte[Sntrup761.SharedSecretSize];
        diagnostics.Arrange("source", "NTRU Prime round 3 KAT/kem/sntrup761/kat_kem.rsp");
        diagnostics.Arrange("count", count);
        diagnostics.Bytes("seed", Convert.FromHexString(vector["seed"]));
        diagnostics.Bytes("key generation random", keyRandom);
        diagnostics.Bytes("encapsulation random", encapsulationRandom);

        bool generated;
        using (diagnostics.Phase("key generation"))
        {
            generated = Sntrup761.TryGenerateKeyPair(keyRandom, publicKey, secretKey);
        }

        using (diagnostics.Phase("encapsulation"))
        {
            Sntrup761.Encapsulate(publicKey, encapsulationRandom, ciphertext, sharedSecret);
        }

        using (diagnostics.Phase("decapsulation"))
        {
            Sntrup761.Decapsulate(secretKey, ciphertext, decapsulated);
        }

        diagnostics.Act("generated", generated);
        diagnostics.Bytes("public key", publicKey);
        diagnostics.Bytes("ciphertext", ciphertext);
        diagnostics.Act("shared secret", Convert.ToHexString(sharedSecret));

        diagnostics.Assert("generated", true, generated);
        diagnostics.Diff("public key", vector["pk"], Convert.ToHexString(publicKey));
        diagnostics.Diff("secret key", vector["sk"], Convert.ToHexString(secretKey));
        diagnostics.Diff("ciphertext", vector["ct"], Convert.ToHexString(ciphertext));
        diagnostics.Diff("shared secret", vector["ss"], Convert.ToHexString(sharedSecret));
        diagnostics.Diff("decapsulated secret", vector["ss"], Convert.ToHexString(decapsulated));
        Assert.IsTrue(generated);
        Assert.AreEqual(vector["pk"], Convert.ToHexString(publicKey));
        Assert.AreEqual(vector["sk"], Convert.ToHexString(secretKey));
        Assert.AreEqual(vector["ct"], Convert.ToHexString(ciphertext));
        Assert.AreEqual(vector["ss"], Convert.ToHexString(sharedSecret));
        Assert.AreEqual(vector["ss"], Convert.ToHexString(decapsulated));
    }

    // Round 3 section 3.3 (Decap): a ciphertext that does not re-encrypt to itself gives
    // HashSession(0, rho, c) = SHA-512(0 || SHA-512(3 || rho)[0..32] || c)[0..32].
    [TestMethod]
    [DataRow(0)]
    [DataRow(Sntrup761.CiphertextSize - 1)]
    public void Decapsulate_TamperedCiphertext_GivesTheImplicitRejectionSecret(int flippedByte)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyDictionary<string, string> vector = ReadKnownAnswer(0);
        byte[] secretKey = Convert.FromHexString(vector["sk"]);
        byte[] ciphertext = Convert.FromHexString(vector["ct"]);
        ciphertext[flippedByte] ^= 0x01;
        byte[] sharedSecret = new byte[Sntrup761.SharedSecretSize];
        diagnostics.Arrange("source", "NTRU Prime round 3 KAT count 0, section 3.3 implicit rejection");
        diagnostics.Arrange("flipped byte", flippedByte);
        diagnostics.Bytes("tampered ciphertext", ciphertext);

        Sntrup761.Decapsulate(secretKey, ciphertext, sharedSecret);
        diagnostics.Act("shared secret", Convert.ToHexString(sharedSecret));

        byte[] rho = secretKey.AsSpan(2 * 191 + Sntrup761.PublicKeySize, 191).ToArray();
        byte[] rhoHash = SHA512.HashData([3, .. rho])[..32];
        byte[] expected = SHA512.HashData([0, .. rhoHash, .. ciphertext])[..32];
        diagnostics.Diff("implicit rejection secret", expected, sharedSecret);
        diagnostics.Assert("differs from the known-answer secret", true, vector["ss"] != Convert.ToHexString(sharedSecret));
        Assert.AreEqual(Convert.ToHexString(expected), Convert.ToHexString(sharedSecret));
        Assert.AreNotEqual(vector["ss"], Convert.ToHexString(sharedSecret));
    }

    [TestMethod]
    public void Decapsulate_FreshKeyPairAndEncapsulation_AgreeOnTheSharedSecret()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] publicKey = new byte[Sntrup761.PublicKeySize];
        byte[] secretKey = new byte[Sntrup761.SecretKeySize];
        byte[] ciphertext = new byte[Sntrup761.CiphertextSize];
        byte[] sharedSecret = new byte[Sntrup761.SharedSecretSize];
        byte[] decapsulated = new byte[Sntrup761.SharedSecretSize];
        diagnostics.Arrange("source", "fresh key pair from the system random number generator");

        using (diagnostics.Phase("key generation"))
        {
            Sntrup761.GenerateKeyPair(publicKey, secretKey);
        }

        using (diagnostics.Phase("encapsulation and decapsulation"))
        {
            Sntrup761.Encapsulate(publicKey, ciphertext, sharedSecret);
            Sntrup761.Decapsulate(secretKey, ciphertext, decapsulated);
        }

        diagnostics.Act("ciphertext length", ciphertext.Length);

        diagnostics.Diff("shared secret", sharedSecret, decapsulated);
        diagnostics.Assert("shared secret is all zero", false, sharedSecret.AsSpan().IndexOfAnyExcept((byte)0) < 0);
        CollectionAssert.AreEqual(sharedSecret, decapsulated);
        CollectionAssert.AreNotEqual(new byte[Sntrup761.SharedSecretSize], sharedSecret);
    }

    // Every word 0x20000000 makes each coefficient of g ((w & 0x3fffffff) * 3 >> 30) - 1 = 0,
    // and the zero polynomial has no inverse modulo 3.
    [TestMethod]
    public void TryGenerateKeyPair_RandomGivingANonInvertibleG_ReturnsFalseWithBothKeysZeroed()
    {
        byte[] publicKey = new byte[Sntrup761.PublicKeySize];
        byte[] secretKey = new byte[Sntrup761.SecretKeySize];
        publicKey.AsSpan().Fill(0xAA);
        secretKey.AsSpan().Fill(0xAA);
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("random", "every g word 0x20000000, so g is zero and has no inverse modulo 3");
        diagnostics.Arrange("keys before", "both filled with 0xAA");

        bool generated = Sntrup761.TryGenerateKeyPair(NonInvertibleKeyRandom(), publicKey, secretKey);
        diagnostics.Act("generated", generated);

        diagnostics.Assert("generated", false, generated);
        diagnostics.Diff("public key", new byte[Sntrup761.PublicKeySize], publicKey);
        diagnostics.Diff("secret key", new byte[Sntrup761.SecretKeySize], secretKey);
        Assert.IsFalse(generated);
        CollectionAssert.AreEqual(new byte[Sntrup761.PublicKeySize], publicKey);
        CollectionAssert.AreEqual(new byte[Sntrup761.SecretKeySize], secretKey);
    }

    [TestMethod]
    public void GenerateKeyPair_FirstRandomGivesANonInvertibleG_DrawsAgainAndSucceeds()
    {
        byte[] publicKey = new byte[Sntrup761.PublicKeySize];
        byte[] secretKey = new byte[Sntrup761.SecretKeySize];
        int draws = 0;
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("random", "first draw gives a non-invertible g, later draws the system random number generator");

        Sntrup761.GenerateKeyPair(publicKey, secretKey, destination =>
        {
            draws++;
            if (draws == 1)
            {
                NonInvertibleKeyRandom().CopyTo(destination);
                return;
            }

            RandomNumberGenerator.Fill(destination);
        });
        diagnostics.Act("draws", draws);

        diagnostics.Assert("draws", 2, draws);
        diagnostics.Diff("public key inside the secret key", publicKey, secretKey.AsSpan(2 * 191, Sntrup761.PublicKeySize));
        Assert.AreEqual(2, draws);
        CollectionAssert.AreEqual(publicKey, secretKey.AsSpan(2 * 191, Sntrup761.PublicKeySize).ToArray());
    }

    [TestMethod]
    [DataRow(Sntrup761.KeyGenerationRandomSize - 1, Sntrup761.PublicKeySize, Sntrup761.SecretKeySize, "random")]
    [DataRow(Sntrup761.KeyGenerationRandomSize, Sntrup761.PublicKeySize + 1, Sntrup761.SecretKeySize, "publicKey")]
    [DataRow(Sntrup761.KeyGenerationRandomSize, Sntrup761.PublicKeySize, Sntrup761.SecretKeySize - 1, "secretKey")]
    public void TryGenerateKeyPair_WrongLength_ThrowsNamingTheParameter(int random, int publicKey, int secretKey, string parameter)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("wrong-length parameter", parameter);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Sntrup761.TryGenerateKeyPair(new byte[random], new byte[publicKey], new byte[secretKey]));
        diagnostics.Act("parameter name", exception.ParamName);

        diagnostics.Assert("parameter name", parameter, exception.ParamName);
        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    [DataRow(Sntrup761.PublicKeySize - 1, Sntrup761.SecretKeySize, "publicKey")]
    [DataRow(Sntrup761.PublicKeySize, Sntrup761.SecretKeySize + 1, "secretKey")]
    public void GenerateKeyPair_WrongLength_ThrowsNamingTheParameter(int publicKey, int secretKey, string parameter)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("wrong-length parameter", parameter);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Sntrup761.GenerateKeyPair(new byte[publicKey], new byte[secretKey]));
        diagnostics.Act("parameter name", exception.ParamName);

        diagnostics.Assert("parameter name", parameter, exception.ParamName);
        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    [DataRow(Sntrup761.PublicKeySize - 1, Sntrup761.EncapsulationRandomSize, Sntrup761.CiphertextSize, Sntrup761.SharedSecretSize, "publicKey")]
    [DataRow(Sntrup761.PublicKeySize, Sntrup761.EncapsulationRandomSize + 1, Sntrup761.CiphertextSize, Sntrup761.SharedSecretSize, "random")]
    [DataRow(Sntrup761.PublicKeySize, Sntrup761.EncapsulationRandomSize, Sntrup761.CiphertextSize - 1, Sntrup761.SharedSecretSize, "ciphertext")]
    [DataRow(Sntrup761.PublicKeySize, Sntrup761.EncapsulationRandomSize, Sntrup761.CiphertextSize, Sntrup761.SharedSecretSize + 1, "sharedSecret")]
    public void Encapsulate_WrongLength_ThrowsNamingTheParameter(int publicKey, int random, int ciphertext, int sharedSecret, string parameter)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("wrong-length parameter", parameter);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Sntrup761.Encapsulate(new byte[publicKey], new byte[random], new byte[ciphertext], new byte[sharedSecret]));
        diagnostics.Act("parameter name", exception.ParamName);

        diagnostics.Assert("parameter name", parameter, exception.ParamName);
        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    [DataRow(Sntrup761.SecretKeySize - 1, Sntrup761.CiphertextSize, Sntrup761.SharedSecretSize, "secretKey")]
    [DataRow(Sntrup761.SecretKeySize, Sntrup761.CiphertextSize + 1, Sntrup761.SharedSecretSize, "ciphertext")]
    [DataRow(Sntrup761.SecretKeySize, Sntrup761.CiphertextSize, Sntrup761.SharedSecretSize - 1, "sharedSecret")]
    public void Decapsulate_WrongLength_ThrowsNamingTheParameter(int secretKey, int ciphertext, int sharedSecret, string parameter)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("wrong-length parameter", parameter);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => Sntrup761.Decapsulate(new byte[secretKey], new byte[ciphertext], new byte[sharedSecret]));
        diagnostics.Act("parameter name", exception.ParamName);

        diagnostics.Assert("parameter name", parameter, exception.ParamName);
        Assert.AreEqual(parameter, exception.ParamName);
    }

    private static byte[] NonInvertibleKeyRandom()
    {
        byte[] random = new byte[Sntrup761.KeyGenerationRandomSize];
        for (int index = 0; index < P; index++)
        {
            random[(4 * index) + 3] = 0x20;
        }

        return random;
    }

    private static IReadOnlyDictionary<string, string> ReadKnownAnswer(int count)
    {
        using Stream stream = typeof(Sntrup761Tests).Assembly.GetManifestResourceStream("KnownAnswers.sntrup761-kat_kem-first-three.txt")!;
        using StreamReader reader = new(stream);
        Dictionary<string, string> vector = [];
        bool inVector = false;
        while (reader.ReadLine() is string line)
        {
            string[] parts = line.Split(" = ", 2);
            if (parts.Length == 2 && parts[0] == "count")
            {
                inVector = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) == count;
            }
            else if (parts.Length == 2 && inVector)
            {
                vector[parts[0]] = parts[1];
            }
        }

        return vector;
    }
}
