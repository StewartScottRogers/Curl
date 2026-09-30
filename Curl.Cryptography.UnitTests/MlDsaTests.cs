using System.Reflection;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="MlDsa" /> to NIST's ACVP ML-DSA known answers (FIPS 204 final) for all
/// three parameter sets - key generation, deterministic and hedged signing, and
/// verification, valid and tampered - plus the input checks and zeroing on dispose.
/// </summary>
[TestClass]
public sealed class MlDsaTests
{
    // KnownAnswers/ml-dsa-acvp-fips204-selected.txt: tests copied unchanged from the ACVP
    // server's ML-DSA-keyGen-FIPS204, ML-DSA-sigGen-FIPS204 and ML-DSA-sigVer-FIPS204
    // internalProjection.json (https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files);
    // the file header names them. sigGen and sigVer cases are pure ML-DSA with a context.
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
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];
        byte[] privateKey = new byte[MlDsa.GetPrivateKeySize(parameterSet)];

        using MlDsa key = MlDsa.GenerateKey(parameterSet, Hex(vector, "seed"));
        key.ExportPublicKey(publicKey);
        key.ExportPrivateKey(privateKey);

        Assert.AreEqual(parameterSet, key.ParameterSet);
        Assert.AreEqual(vector["pk"], Convert.ToHexString(publicKey));
        Assert.AreEqual(vector["sk"], Convert.ToHexString(privateKey));
    }

    [TestMethod]
    [DataRow("keyGen 2")]
    [DataRow("keyGen 27")]
    [DataRow("keyGen 52")]
    public void ImportPrivateKey_AcvpKeyGenVector_RecomputesThePublicKey(string caseName)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];

        using MlDsa key = MlDsa.ImportPrivateKey(parameterSet, Hex(vector, "sk"));
        key.ExportPublicKey(publicKey);

        Assert.AreEqual(vector["pk"], Convert.ToHexString(publicKey));
    }

    [TestMethod]
    [DataRow("sigGen 2")]
    [DataRow("sigGen 7")]
    [DataRow("sigGen 35")]
    [DataRow("sigGen 41")]
    [DataRow("sigGen 71")]
    [DataRow("sigGen 72")]
    public void SignDataDeterministic_AcvpSigGenVector_MatchesTheSignature(string caseName)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] signature = new byte[MlDsa.GetSignatureSize(parameterSet)];

        using MlDsa key = MlDsa.ImportPrivateKey(parameterSet, Hex(vector, "sk"));
        key.SignDataDeterministic(Hex(vector, "message"), Hex(vector, "context"), signature);

        Assert.AreEqual(vector["signature"], Convert.ToHexString(signature));
    }

    [TestMethod]
    [DataRow("sigGen 194")]
    [DataRow("sigGen 214")]
    [DataRow("sigGen 251")]
    public void SignData_AcvpHedgedSigGenVector_MatchesTheSignature(string caseName)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] signature = new byte[MlDsa.GetSignatureSize(parameterSet)];

        using MlDsa key = MlDsa.ImportPrivateKey(parameterSet, Hex(vector, "sk"));
        key.SignData(Hex(vector, "message"), Hex(vector, "context"), Hex(vector, "rnd"), signature);

        Assert.AreEqual(vector["signature"], Convert.ToHexString(signature));
    }

    // Each parameter set has one valid case and one each of a modified message, commitment
    // hash, z and hint.
    [TestMethod]
    [DataRow("sigVer 5")]
    [DataRow("sigVer 8")]
    [DataRow("sigVer 9")]
    [DataRow("sigVer 11")]
    [DataRow("sigVer 14")]
    [DataRow("sigVer 31")]
    [DataRow("sigVer 34")]
    [DataRow("sigVer 35")]
    [DataRow("sigVer 38")]
    [DataRow("sigVer 43")]
    [DataRow("sigVer 65")]
    [DataRow("sigVer 66")]
    [DataRow("sigVer 68")]
    [DataRow("sigVer 70")]
    [DataRow("sigVer 75")]
    public void VerifyData_AcvpSigVerVector_GivesTheExpectedResult(string caseName)
    {
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);

        bool verified = MlDsa.VerifyData(parameterSet, Hex(vector, "pk"), Hex(vector, "message"), Hex(vector, "context"), Hex(vector, "signature"));

        Assert.AreEqual(bool.Parse(vector["testPassed"]), verified, vector["reason"]);
    }

    [TestMethod]
    [DataRow(MlDsaParameterSet.MlDsa44, 1312, 2560, 2420)]
    [DataRow(MlDsaParameterSet.MlDsa65, 1952, 4032, 3309)]
    [DataRow(MlDsaParameterSet.MlDsa87, 2592, 4896, 4627)]
    public void Sizes_EachParameterSet_MatchFips204Table2(MlDsaParameterSet parameterSet, int publicKeySize, int privateKeySize, int signatureSize)
    {
        Assert.AreEqual(publicKeySize, MlDsa.GetPublicKeySize(parameterSet));
        Assert.AreEqual(privateKeySize, MlDsa.GetPrivateKeySize(parameterSet));
        Assert.AreEqual(signatureSize, MlDsa.GetSignatureSize(parameterSet));
    }

    [TestMethod]
    [DataRow(MlDsaParameterSet.MlDsa44)]
    [DataRow(MlDsaParameterSet.MlDsa65)]
    [DataRow(MlDsaParameterSet.MlDsa87)]
    public void VerifyData_RandomKeyAndHedgedSignature_Verifies(MlDsaParameterSet parameterSet)
    {
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];
        byte[] signature = new byte[MlDsa.GetSignatureSize(parameterSet)];
        byte[] message = "ML-DSA round trip"u8.ToArray();
        byte[] context = "curl"u8.ToArray();
        using MlDsa key = MlDsa.GenerateKey(parameterSet);
        key.ExportPublicKey(publicKey);

        key.SignData(message, context, signature);

        Assert.IsTrue(MlDsa.VerifyData(parameterSet, publicKey, message, context, signature));
        Assert.IsFalse(MlDsa.VerifyData(parameterSet, publicKey, message, "curL"u8, signature));
    }

    [TestMethod]
    public void SignData_TwiceWithFreshRandomness_GivesDifferentSignaturesThatBothVerify()
    {
        byte[] publicKey = new byte[1312];
        byte[] first = new byte[2420];
        byte[] second = new byte[2420];
        using MlDsa key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, new byte[32]);
        key.ExportPublicKey(publicKey);

        key.SignData("m"u8, [], first);
        key.SignData("m"u8, [], second);

        CollectionAssert.AreNotEqual(first, second);
        Assert.IsTrue(MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, publicKey, "m"u8, [], first));
        Assert.IsTrue(MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, publicKey, "m"u8, [], second));
    }

    [TestMethod]
    public void SignDataDeterministic_Twice_GivesTheSameSignature()
    {
        byte[] first = new byte[2420];
        byte[] second = new byte[2420];
        using MlDsa key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, new byte[32]);

        key.SignDataDeterministic("m"u8, "c"u8, first);
        key.SignDataDeterministic("m"u8, "c"u8, second);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void VerifyData_ZOutOfRange_ReturnsFalse()
    {
        Dictionary<string, string> vector = Vectors["sigVer 11"];
        byte[] signature = Hex(vector, "signature");
        Array.Clear(signature, 32, 8); // z packed as gamma1 - z: zero bytes are z = gamma1

        bool verified = MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, Hex(vector, "pk"), Hex(vector, "message"), Hex(vector, "context"), signature);

        Assert.IsFalse(verified);
    }

    [TestMethod]
    public void VerifyData_HintCountPastOmega_ReturnsFalse()
    {
        Dictionary<string, string> vector = Vectors["sigVer 11"];
        byte[] signature = Hex(vector, "signature");
        signature[^1] = 81; // omega is 80 for ML-DSA-44

        bool verified = MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, Hex(vector, "pk"), Hex(vector, "message"), Hex(vector, "context"), signature);

        Assert.IsFalse(verified);
    }

    [TestMethod]
    public void ImportPrivateKey_TrOrT0Altered_ThrowsArgumentException()
    {
        byte[] privateKey = Hex(Vectors["keyGen 1"], "sk");
        byte[] trAltered = (byte[])privateKey.Clone();
        byte[] t0Altered = (byte[])privateKey.Clone();
        trAltered[64] ^= 0x01;
        t0Altered[^1] ^= 0x01;

        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.ImportPrivateKey(MlDsaParameterSet.MlDsa44, trAltered));
        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.ImportPrivateKey(MlDsaParameterSet.MlDsa44, t0Altered));
    }

    [TestMethod]
    public void EveryMember_UndefinedParameterSet_ThrowsArgumentOutOfRangeException()
    {
        const MlDsaParameterSet undefined = (MlDsaParameterSet)3;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.GetPublicKeySize(undefined));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.GenerateKey(undefined));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.ImportPrivateKey(undefined, []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.VerifyData(undefined, [], [], [], []));
    }

    [TestMethod]
    public void EveryMember_WrongLengthOrContextTooLong_ThrowsArgumentException()
    {
        const MlDsaParameterSet set = MlDsaParameterSet.MlDsa44;
        byte[] publicKey = new byte[1312];
        byte[] signature = new byte[2420];
        byte[] longContext = new byte[256];
        using MlDsa key = MlDsa.GenerateKey(set, new byte[32]);

        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.GenerateKey(set, new byte[31]));
        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.ImportPrivateKey(set, new byte[2559]));
        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.VerifyData(set, new byte[1311], [], [], signature));
        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.VerifyData(set, publicKey, [], [], new byte[2421]));
        Assert.ThrowsExactly<ArgumentException>(() => MlDsa.VerifyData(set, publicKey, [], longContext, signature));
        Assert.ThrowsExactly<ArgumentException>(() => key.ExportPublicKey(new byte[1313]));
        Assert.ThrowsExactly<ArgumentException>(() => key.ExportPrivateKey(new byte[2561]));
        Assert.ThrowsExactly<ArgumentException>(() => key.SignData([], [], new byte[31], signature));
        Assert.ThrowsExactly<ArgumentException>(() => key.SignData([], [], new byte[2419]));
        Assert.ThrowsExactly<ArgumentException>(() => key.SignDataDeterministic([], longContext, signature));
    }

    [TestMethod]
    public void Dispose_ZeroesThePrivateKeyAndRefusesEveryLaterCall()
    {
        MlDsa key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, new byte[32]);
        byte[] held = (byte[])typeof(MlDsa).GetField("privateKey", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(key)!;

        key.Dispose();

        Assert.IsTrue(held.All(value => value == 0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportPublicKey(new byte[1312]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportPrivateKey(new byte[2560]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.SignDataDeterministic([], [], new byte[2420]));
    }

    private static MlDsaParameterSet ParseParameterSet(Dictionary<string, string> vector) => vector["parameterSet"] switch
    {
        "ML-DSA-44" => MlDsaParameterSet.MlDsa44,
        "ML-DSA-65" => MlDsaParameterSet.MlDsa65,
        _ => MlDsaParameterSet.MlDsa87,
    };

    private static byte[] Hex(Dictionary<string, string> vector, string field) => Convert.FromHexString(vector[field]);

    private static Dictionary<string, Dictionary<string, string>> ReadVectors()
    {
        using Stream stream = typeof(MlDsaTests).Assembly.GetManifestResourceStream("KnownAnswers.ml-dsa-acvp-fips204-selected.txt")!;
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
