using System.Reflection;

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

    [TestMethod]
    [DataRow("keyGen 1")]
    [DataRow("keyGen 2")]
    [DataRow("keyGen 26")]
    [DataRow("keyGen 27")]
    [DataRow("keyGen 51")]
    [DataRow("keyGen 52")]
    public void GenerateKey_AcvpKeyGenVector_MatchesBothKeys(string caseName)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        byte[] decapsulationKey = new byte[MlKem.GetDecapsulationKeySize(parameterSet)];

        using MlKem key = MlKem.GenerateKey(parameterSet, Hex(vector, "d"), Hex(vector, "z"));
        key.ExportEncapsulationKey(encapsulationKey);
        key.ExportDecapsulationKey(decapsulationKey);

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
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];

        bool encapsulated = MlKem.TryEncapsulate(parameterSet, Hex(vector, "ek"), Hex(vector, "m"), ciphertext, sharedSecret);

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
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];

        using MlKem key = MlKem.ImportDecapsulationKey(parameterSet, Hex(vector, "dk"));
        key.Decapsulate(Hex(vector, "c"), sharedSecret);

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
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] decapsulationKey = Hex(vector, "dk");

        if (bool.Parse(vector["testPassed"]))
        {
            using MlKem key = MlKem.ImportDecapsulationKey(parameterSet, decapsulationKey);
            Assert.AreEqual(parameterSet, key.ParameterSet);
        }
        else
        {
            Assert.AreEqual("modified H", vector["reason"]);
            Assert.ThrowsExactly<ArgumentException>(() => MlKem.ImportDecapsulationKey(parameterSet, decapsulationKey));
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
        Dictionary<string, string> vector = Vectors[caseName];
        MlKemParameterSet parameterSet = ParseParameterSet(vector);
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        Array.Fill(ciphertext, (byte)0xFF);
        Array.Fill(sharedSecret, (byte)0xFF);

        bool encapsulated = MlKem.TryEncapsulate(parameterSet, Hex(vector, "ek"), new byte[MlKem.MessageSize], ciphertext, sharedSecret);

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
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(parameterSet)];
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(parameterSet)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        byte[] decapsulated = new byte[MlKem.SharedSecretSize];
        using MlKem key = MlKem.GenerateKey(parameterSet);
        key.ExportEncapsulationKey(encapsulationKey);

        bool encapsulated = MlKem.TryEncapsulate(parameterSet, encapsulationKey, ciphertext, sharedSecret);
        key.Decapsulate(ciphertext, decapsulated);

        Assert.IsTrue(encapsulated);
        CollectionAssert.AreEqual(sharedSecret, decapsulated);
        Assert.IsFalse(sharedSecret.All(value => value == 0));
    }

    [TestMethod]
    public void Decapsulate_OneCiphertextBitFlipped_GivesADifferentKey()
    {
        byte[] encapsulationKey = new byte[MlKem.GetEncapsulationKeySize(MlKemParameterSet.MlKem768)];
        byte[] ciphertext = new byte[MlKem.GetCiphertextSize(MlKemParameterSet.MlKem768)];
        byte[] sharedSecret = new byte[MlKem.SharedSecretSize];
        byte[] decapsulated = new byte[MlKem.SharedSecretSize];
        using MlKem key = MlKem.GenerateKey(MlKemParameterSet.MlKem768);
        key.ExportEncapsulationKey(encapsulationKey);
        MlKem.TryEncapsulate(MlKemParameterSet.MlKem768, encapsulationKey, ciphertext, sharedSecret);
        ciphertext[^1] ^= 0x01;

        key.Decapsulate(ciphertext, decapsulated);

        CollectionAssert.AreNotEqual(sharedSecret, decapsulated);
    }

    [TestMethod]
    public void EveryMember_UndefinedParameterSet_ThrowsArgumentOutOfRangeException()
    {
        const MlKemParameterSet undefined = (MlKemParameterSet)3;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.GetEncapsulationKeySize(undefined));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.GenerateKey(undefined));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.ImportDecapsulationKey(undefined, []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKem.TryEncapsulate(undefined, [], [], []));
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

        Assert.ThrowsExactly<ArgumentException>(() => MlKem.GenerateKey(set, new byte[31], seed));
        Assert.ThrowsExactly<ArgumentException>(() => MlKem.GenerateKey(set, seed, new byte[33]));
        Assert.ThrowsExactly<ArgumentException>(() => MlKem.ImportDecapsulationKey(set, new byte[1631]));
        Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, new byte[799], ciphertext, sharedSecret));
        Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, encapsulationKey, new byte[31], ciphertext, sharedSecret));
        Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, encapsulationKey, new byte[767], sharedSecret));
        Assert.ThrowsExactly<ArgumentException>(() => MlKem.TryEncapsulate(set, encapsulationKey, ciphertext, new byte[31]));
        Assert.ThrowsExactly<ArgumentException>(() => key.ExportEncapsulationKey(new byte[801]));
        Assert.ThrowsExactly<ArgumentException>(() => key.ExportDecapsulationKey(new byte[1631]));
        Assert.ThrowsExactly<ArgumentException>(() => key.Decapsulate(new byte[769], sharedSecret));
        Assert.ThrowsExactly<ArgumentException>(() => key.Decapsulate(ciphertext, new byte[33]));
    }

    [TestMethod]
    public void Dispose_ZeroesTheDecapsulationKeyAndRefusesEveryLaterCall()
    {
        byte[] seed = new byte[32];
        Array.Fill(seed, (byte)0x5A);
        MlKem key = MlKem.GenerateKey(MlKemParameterSet.MlKem512, seed, seed);
        byte[] held = (byte[])typeof(MlKem).GetField("decapsulationKey", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(key)!;

        key.Dispose();

        Assert.IsTrue(held.All(value => value == 0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportEncapsulationKey(new byte[800]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportDecapsulationKey(new byte[1632]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.Decapsulate(new byte[768], new byte[32]));
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
