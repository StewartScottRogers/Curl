using System.Reflection;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="MlKem" /> to NIST's ACVP ML-KEM known answers (FIPS 203 final) for all
/// three parameter sets, including decapsulation of modified ciphertexts to the
/// implicit-rejection key and FIPS 203 section 7's input checks.
/// </summary>
[TestClass]
public sealed class MlKemTests
{
    // KnownAnswers/ml-kem-acvp-fips203-selected.txt: tests copied unchanged from the ACVP
    // server's ML-KEM-keyGen-FIPS203 and ML-KEM-encapDecap-FIPS203 internalProjection.json
    // (https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files); the file
    // header names them.
    private static readonly Dictionary<string, Dictionary<string, string>> Vectors = ReadVectors();

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("keyGen 1")]
    [DataRow("keyGen 2")]
    [DataRow("keyGen 26")]
    [DataRow("keyGen 27")]
    [DataRow("keyGen 51")]
    [DataRow("keyGen 52")]
    public void GenerateKey_AcvpKeyGenVector_MatchesBothKeys(string caseName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        byte[] decapsulationKey = new byte[MlKem.GetDecapsulationKeySize(parameterSet)];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Bytes("d", Hex(vector, "d"));
        diagnostics.Bytes("z", Hex(vector, "z"));

        using MlKem key = MlKem.GenerateKey(parameterSet, Hex(vector, "d"), Hex(vector, "z"));
        key.ExportEncapsulationKey(encapsulationKey);
        key.ExportDecapsulationKey(decapsulationKey);
        diagnostics.Act("parameter set", key.ParameterSet);
        diagnostics.Bytes("encapsulation key", encapsulationKey);
        diagnostics.Bytes("decapsulation key", decapsulationKey);

        diagnostics.Assert("parameter set", parameterSet, key.ParameterSet);
        diagnostics.Diff("encapsulation key", Hex(vector, "ek"), encapsulationKey);
        diagnostics.Diff("decapsulation key", Hex(vector, "dk"), decapsulationKey);
        Assert.AreEqual(parameterSet, key.ParameterSet);
        Assert.AreEqual(vector["ek"], Convert.ToHexString(encapsulationKey));
        Assert.AreEqual(vector["dk"], Convert.ToHexString(decapsulationKey));
    }

    [TestMethod]
    [DataRow("encapsulation 1")]
    [DataRow("encapsulation 26")]
    [DataRow("encapsulation 51")]
    public void TryEncapsulate_AcvpEncapsulationVector_MatchesCiphertextAndSharedSecret(string caseName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Bytes("encapsulation key", Hex(vector, "ek"));
        diagnostics.Bytes("m", Hex(vector, "m"));

        bool encapsulated = MlKem.TryEncapsulate(parameterSet, Hex(vector, "ek"), Hex(vector, "m"), ciphertext, sharedSecret);
        diagnostics.Act("encapsulated", encapsulated);
        diagnostics.Bytes("ciphertext", ciphertext);
        diagnostics.Bytes("shared secret", sharedSecret);

        diagnostics.Assert("encapsulated", true, encapsulated);
        diagnostics.Diff("ciphertext", Hex(vector, "c"), ciphertext);
        diagnostics.Diff("shared secret", Hex(vector, "k"), sharedSecret);
        Assert.IsTrue(encapsulated);
        Assert.AreEqual(vector["c"], Convert.ToHexString(ciphertext));
        Assert.AreEqual(vector["k"], Convert.ToHexString(sharedSecret));
    }

    // Each parameter set has one "valid decapsulation" and one "modified ciphertext" case;
    // the modified one's k is the implicit-rejection key J(z || c).
    [TestMethod]
    [DataRow("decapsulation 76", "modified ciphertext")]
    [DataRow("decapsulation 79", "valid decapsulation")]
    [DataRow("decapsulation 86", "valid decapsulation")]
    [DataRow("decapsulation 88", "modified ciphertext")]
    [DataRow("decapsulation 96", "modified ciphertext")]
    [DataRow("decapsulation 97", "valid decapsulation")]
    public void Decapsulate_AcvpDecapsulationVector_MatchesSharedSecretOrRejectionKey(string caseName, string reason)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Arrange("reason", vector["reason"]);
        diagnostics.Bytes("decapsulation key", Hex(vector, "dk"));
        diagnostics.Bytes("ciphertext", Hex(vector, "c"));

        using MlKem key = MlKem.ImportDecapsulationKey(parameterSet, Hex(vector, "dk"));
        key.Decapsulate(Hex(vector, "c"), sharedSecret);
        diagnostics.Act("shared secret", Convert.ToHexString(sharedSecret));

        diagnostics.Assert("reason", reason, vector["reason"]);
        diagnostics.Diff("shared secret", Hex(vector, "k"), sharedSecret);
        Assert.AreEqual(reason, vector["reason"]);
        Assert.AreEqual(vector["k"], Convert.ToHexString(sharedSecret));
    }

    [TestMethod]
    [DataRow("decapsulationKeyCheck 106")]
    [DataRow("decapsulationKeyCheck 108")]
    [DataRow("decapsulationKeyCheck 126")]
    [DataRow("decapsulationKeyCheck 128")]
    [DataRow("decapsulationKeyCheck 146")]
    [DataRow("decapsulationKeyCheck 148")]
    public void ImportDecapsulationKey_AcvpKeyCheckVector_AcceptsOnlyAMatchingHash(string caseName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] decapsulationKey = Hex(vector, "dk");
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Arrange("test passed", vector["testPassed"]);
        diagnostics.Bytes("decapsulation key", decapsulationKey);

        if (bool.Parse(vector["testPassed"]))
        {
            using MlKem key = MlKem.ImportDecapsulationKey(parameterSet, decapsulationKey);
            diagnostics.Act("imported parameter set", key.ParameterSet);
            diagnostics.Assert("imported parameter set", parameterSet, key.ParameterSet);
            Assert.AreEqual(parameterSet, key.ParameterSet);
        }
        else
        {
            diagnostics.Assert("reason", "modified H", vector["reason"]);
            Assert.AreEqual("modified H", vector["reason"]);
            diagnostics.Act("import exception", Assert.ThrowsExactly<ArgumentException>(() => MlKem.ImportDecapsulationKey(parameterSet, decapsulationKey)).GetType().Name);
        }
    }

    [TestMethod]
    [DataRow("encapsulationKeyCheck 116")]
    [DataRow("encapsulationKeyCheck 117")]
    [DataRow("encapsulationKeyCheck 136")]
    [DataRow("encapsulationKeyCheck 137")]
    [DataRow("encapsulationKeyCheck 156")]
    [DataRow("encapsulationKeyCheck 159")]
    public void TryEncapsulate_AcvpKeyCheckVector_RefusesACoefficientOfQOrMore(string caseName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        Array.Fill(ciphertext, (byte)0xFF);
        Array.Fill(sharedSecret, (byte)0xFF);
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Arrange("test passed", vector["testPassed"]);
        diagnostics.Bytes("encapsulation key", Hex(vector, "ek"));
        diagnostics.Arrange("m and destination fill", "32 zero bytes; destinations filled with 0xFF");

        bool encapsulated = MlKem.TryEncapsulate(parameterSet, Hex(vector, "ek"), new byte[MlKem.MessageSize], ciphertext, sharedSecret);
        diagnostics.Act("encapsulated", encapsulated);
        diagnostics.Bytes("ciphertext", ciphertext);
        diagnostics.Bytes("shared secret", sharedSecret);

        diagnostics.Assert("encapsulated", bool.Parse(vector["testPassed"]), encapsulated);
        diagnostics.Assert("ciphertext and shared secret not all zero", $"{encapsulated}, {encapsulated}", $"{!ciphertext.All(value => value == 0)}, {!sharedSecret.All(value => value == 0)}");
        Assert.AreEqual(bool.Parse(vector["testPassed"]), encapsulated);
        Assert.AreEqual(encapsulated, !ciphertext.All(value => value == 0));
        Assert.AreEqual(encapsulated, !sharedSecret.All(value => value == 0));
    }

    [TestMethod]
    [DataRow(MlKemParameterSet.MlKem512, 800, 1632, 768)]
    [DataRow(MlKemParameterSet.MlKem768, 1184, 2400, 1088)]
    [DataRow(MlKemParameterSet.MlKem1024, 1568, 3168, 1568)]
    public void Sizes_EachParameterSet_MatchFips203Table3(MlKemParameterSet parameterSet, int encapsulationKeySize, int decapsulationKeySize, int ciphertextSize)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "FIPS 203 table 3");
        diagnostics.Arrange("parameter set", parameterSet);

        string sizes = $"{MlKem.GetEncapsulationKeySize(parameterSet)}, {MlKem.GetDecapsulationKeySize(parameterSet)}, {MlKem.GetCiphertextSize(parameterSet)}";
        diagnostics.Act("encapsulation key, decapsulation key and ciphertext sizes", sizes);

        diagnostics.Assert("encapsulation key, decapsulation key and ciphertext sizes", $"{encapsulationKeySize}, {decapsulationKeySize}, {ciphertextSize}", sizes);
        Assert.AreEqual(encapsulationKeySize, MlKem.GetEncapsulationKeySize(parameterSet));
        Assert.AreEqual(decapsulationKeySize, MlKem.GetDecapsulationKeySize(parameterSet));
        Assert.AreEqual(ciphertextSize, MlKem.GetCiphertextSize(parameterSet));
    }

    [TestMethod]
    [DataRow(MlKemParameterSet.MlKem512)]
    [DataRow(MlKemParameterSet.MlKem768)]
    [DataRow(MlKemParameterSet.MlKem1024)]
    public void Decapsulate_RandomKeyAndEncapsulation_RecoversTheSharedSecret(MlKemParameterSet parameterSet)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        byte[] decapsulated = new byte[MlKem.SharedSecretSize];
        using MlKem key = MlKem.GenerateKey(parameterSet);
        key.ExportEncapsulationKey(encapsulationKey);
        diagnostics.Arrange("parameter set", parameterSet);
        diagnostics.Arrange("key and m", "random");

        bool encapsulated = MlKem.TryEncapsulate(parameterSet, encapsulationKey, ciphertext, sharedSecret);
        key.Decapsulate(ciphertext, decapsulated);
        diagnostics.Act("encapsulated", encapsulated);

        diagnostics.Assert("encapsulated", true, encapsulated);
        diagnostics.Diff("decapsulated shared secret", sharedSecret, decapsulated);
        diagnostics.Assert("shared secret all zero", false, sharedSecret.All(value => value == 0));
        Assert.IsTrue(encapsulated);
        CollectionAssert.AreEqual(sharedSecret, decapsulated);
        Assert.IsFalse(sharedSecret.All(value => value == 0));
    }

    [TestMethod]
    public void Decapsulate_OneCiphertextBitFlipped_GivesADifferentKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(MlKemParameterSet.MlKem768)];
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(MlKemParameterSet.MlKem768)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        byte[] decapsulated = new byte[MlKem.SharedSecretSize];
        using MlKem key = MlKem.GenerateKey(MlKemParameterSet.MlKem768);
        key.ExportEncapsulationKey(encapsulationKey);
        MlKem.TryEncapsulate(MlKemParameterSet.MlKem768, encapsulationKey, ciphertext, sharedSecret);
        ciphertext[^1] ^= 0x01;
        diagnostics.Arrange("parameter set", MlKemParameterSet.MlKem768);
        diagnostics.Arrange("ciphertext", "a random encapsulation with bit 0 of its last byte flipped");

        key.Decapsulate(ciphertext, decapsulated);
        diagnostics.Act("decapsulated equals the shared secret", sharedSecret.AsSpan().SequenceEqual(decapsulated));

        diagnostics.Diff("decapsulated against the shared secret (expected to differ)", sharedSecret, decapsulated);
        CollectionAssert.AreNotEqual(sharedSecret, decapsulated);
    }

    [TestMethod]
    public void EveryMember_UndefinedParameterSet_ThrowsArgumentOutOfRangeException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const MlKemParameterSet undefined = (MlKemParameterSet)3;
        diagnostics.Arrange("parameter set", (int)undefined);

        diagnostics.Act("GetEncapsulationKeySize", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.GetEncapsulationKeySize(undefined)).GetType().Name);
        diagnostics.Act("GenerateKey", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.GenerateKey(undefined)).GetType().Name);
        diagnostics.Act("ImportDecapsulationKey", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.ImportDecapsulationKey(undefined, [])).GetType().Name);
        diagnostics.Act("TryEncapsulate", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.TryEncapsulate(undefined, [], [], [])).GetType().Name);

        diagnostics.Assert("calls that threw ArgumentOutOfRangeException", 4, 4);
    }

    [TestMethod]
    public void EveryMember_WrongLength_ThrowsArgumentException()
    {
        const MlKemParameterSet set = MlKemParameterSet.MlKem512;
        byte[] seed = new byte[32];
        byte[] encapsulationKey = new byte[800];
        byte[] ciphertext = new byte[768];
        byte[] sharedSecret = new byte[32];
        using MlKem key = MlKem.GenerateKey(set, seed, seed);
        key.ExportEncapsulationKey(encapsulationKey);
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("parameter set", set);
        diagnostics.Arrange("right lengths", "seeds 32, ek 800, dk 1632, m 32, ciphertext 768, shared secret 32");

        diagnostics.Act("GenerateKey, 31-byte d", Assert.ThrowsExactly<ArgumentException>(() => MlKem.GenerateKey(set, new byte[31], seed)).GetType().Name);
        diagnostics.Act("GenerateKey, 33-byte z", Assert.ThrowsExactly<ArgumentException>(() => MlKem.GenerateKey(set, seed, new byte[33])).GetType().Name);
        diagnostics.Act("ImportDecapsulationKey, 1631 bytes", Assert.ThrowsExactly<ArgumentException>(() => MlKem.ImportDecapsulationKey(set, new byte[1631])).GetType().Name);
        diagnostics.Act("TryEncapsulate, 799-byte ek", Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, new byte[799], ciphertext, sharedSecret)).GetType().Name);
        diagnostics.Act("TryEncapsulate, 31-byte m", Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, encapsulationKey, new byte[31], ciphertext, sharedSecret)).GetType().Name);
        diagnostics.Act("TryEncapsulate, 767-byte ciphertext", Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, encapsulationKey, new byte[767], sharedSecret)).GetType().Name);
        diagnostics.Act("TryEncapsulate, 31-byte shared secret", Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, encapsulationKey, ciphertext, new byte[31])).GetType().Name);
        diagnostics.Act("ExportEncapsulationKey, 801 bytes", Assert.ThrowsExactly<ArgumentException>(() => key.ExportEncapsulationKey(new byte[801])).GetType().Name);
        diagnostics.Act("ExportDecapsulationKey, 1631 bytes", Assert.ThrowsExactly<ArgumentException>(() => key.ExportDecapsulationKey(new byte[1631])).GetType().Name);
        diagnostics.Act("Decapsulate, 769-byte ciphertext", Assert.ThrowsExactly<ArgumentException>(() => key.Decapsulate(new byte[769], sharedSecret)).GetType().Name);
        diagnostics.Act("Decapsulate, 33-byte shared secret", Assert.ThrowsExactly<ArgumentException>(() => key.Decapsulate(ciphertext, new byte[33])).GetType().Name);

        diagnostics.Assert("calls that threw ArgumentException", 11, 11);
    }

    [TestMethod]
    public void Dispose_ZeroesTheDecapsulationKeyAndRefusesEveryLaterCall()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] seed = new byte[32];
        Array.Fill(seed, (byte)0x5A);
        MlKem key = MlKem.GenerateKey(MlKemParameterSet.MlKem512, seed, seed);
        byte[] held = (byte[])typeof(MlKem).GetField("decapsulationKey", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(key)!;
        diagnostics.Arrange("parameter set", MlKemParameterSet.MlKem512);
        diagnostics.Bytes("seeds d and z", seed);

        key.Dispose();
        diagnostics.Act("held decapsulation key all zero", held.All(value => value == 0));

        diagnostics.Assert("held decapsulation key all zero", true, held.All(value => value == 0));
        Assert.IsTrue(held.All(value => value == 0));
        diagnostics.Act("ExportEncapsulationKey", Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportEncapsulationKey(new byte[800])).GetType().Name);
        diagnostics.Act("ExportDecapsulationKey", Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportDecapsulationKey(new byte[1632])).GetType().Name);
        diagnostics.Act("Decapsulate", Assert.ThrowsExactly<ObjectDisposedException>(() => key.Decapsulate(new byte[768], new byte[32])).GetType().Name);
    }

    private static void ArrangeVector(TestDiagnostics diagnostics, string caseName, MlKemParameterSet parameterSet)
    {
        diagnostics.Arrange("vector source", $"NIST ACVP ML-KEM FIPS 203, KnownAnswers/ml-kem-acvp-fips203-selected.txt case {caseName}");
        diagnostics.Arrange("parameter set", parameterSet);
    }

    private static MlKemParameterSet ParseParameterSet(Dictionary<string, string> vector) => vector["parameterSet"] switch
    {
        "ML-KEM-512" => MlKemParameterSet.MlKem512,
        "ML-KEM-768" => MlKemParameterSet.MlKem768,
        _ => MlKemParameterSet.MlKem1024,
    };

    private static byte[] Hex(Dictionary<string, string> vector, string field) => Convert.FromHexString(vector[field]);

    private static Dictionary<string, Dictionary<string, string>> ReadVectors()
    {
        using Stream stream = typeof(MlKemTests).Assembly.GetManifestResourceStream("KnownAnswers.ml-kem-acvp-fips203-selected.txt")!;
        using StreamReader reader = new(stream);
        Dictionary<string, Dictionary<string, string>> vectors = [];
        Dictionary<string, string> current = [];
        while (reader.ReadLine() is string line)
        {
            string[] parts = line.Split(" = ", 2);
            if (parts[0] == "case")
            {
                current = [];
                vectors[parts[1]] = current;
            }
            else if (parts.Length == 2)
            {
                current[parts[0]] = parts[1];
            }
        }

        return vectors;
    }
}
