using System.Reflection;
using Curl.Testing;

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
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];
        byte[] privateKey = new byte[MlDsa.GetPrivateKeySize(parameterSet)];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Bytes("seed", Hex(vector, "seed"));

        using MlDsa key = MlDsa.GenerateKey(parameterSet, Hex(vector, "seed"));
        key.ExportPublicKey(publicKey);
        key.ExportPrivateKey(privateKey);
        diagnostics.Act("parameter set", key.ParameterSet);
        diagnostics.Bytes("public key", publicKey);
        diagnostics.Bytes("private key", privateKey);

        diagnostics.Assert("parameter set", parameterSet, key.ParameterSet);
        diagnostics.Diff("public key", Hex(vector, "pk"), publicKey);
        diagnostics.Diff("private key", Hex(vector, "sk"), privateKey);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Bytes("private key", Hex(vector, "sk"));

        using MlDsa key = MlDsa.ImportPrivateKey(parameterSet, Hex(vector, "sk"));
        key.ExportPublicKey(publicKey);
        diagnostics.Act("public key", Convert.ToHexString(publicKey.AsSpan(0, 32)) + "...");

        diagnostics.Diff("public key", Hex(vector, "pk"), publicKey);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] signature = new byte[MlDsa.GetSignatureSize(parameterSet)];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Bytes("message", Hex(vector, "message"));
        diagnostics.Bytes("context", Hex(vector, "context"));

        using MlDsa key = MlDsa.ImportPrivateKey(parameterSet, Hex(vector, "sk"));
        key.SignDataDeterministic(Hex(vector, "message"), Hex(vector, "context"), signature);
        diagnostics.Bytes("signature", signature);
        diagnostics.Act("signature length", signature.Length);

        diagnostics.Diff("signature", Hex(vector, "signature"), signature);
        Assert.AreEqual(vector["signature"], Convert.ToHexString(signature));
    }

    [TestMethod]
    [DataRow("sigGen 194")]
    [DataRow("sigGen 214")]
    [DataRow("sigGen 251")]
    public void SignData_AcvpHedgedSigGenVector_MatchesTheSignature(string caseName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        byte[] signature = new byte[MlDsa.GetSignatureSize(parameterSet)];
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Bytes("message", Hex(vector, "message"));
        diagnostics.Bytes("context", Hex(vector, "context"));
        diagnostics.Bytes("rnd", Hex(vector, "rnd"));

        using MlDsa key = MlDsa.ImportPrivateKey(parameterSet, Hex(vector, "sk"));
        key.SignData(Hex(vector, "message"), Hex(vector, "context"), Hex(vector, "rnd"), signature);
        diagnostics.Bytes("signature", signature);
        diagnostics.Act("signature length", signature.Length);

        diagnostics.Diff("signature", Hex(vector, "signature"), signature);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors[caseName];
        MlDsaParameterSet parameterSet = ParseParameterSet(vector);
        ArrangeVector(diagnostics, caseName, parameterSet);
        diagnostics.Arrange("reason", vector["reason"]);
        diagnostics.Bytes("message", Hex(vector, "message"));
        diagnostics.Bytes("context", Hex(vector, "context"));
        diagnostics.Bytes("signature", Hex(vector, "signature"));

        bool verified = MlDsa.VerifyData(parameterSet, Hex(vector, "pk"), Hex(vector, "message"), Hex(vector, "context"), Hex(vector, "signature"));
        diagnostics.Act("verified", verified);

        diagnostics.Assert("verified", bool.Parse(vector["testPassed"]), verified);
        Assert.AreEqual(bool.Parse(vector["testPassed"]), verified, vector["reason"]);
    }

    [TestMethod]
    [DataRow(MlDsaParameterSet.MlDsa44, 1312, 2560, 2420)]
    [DataRow(MlDsaParameterSet.MlDsa65, 1952, 4032, 3309)]
    [DataRow(MlDsaParameterSet.MlDsa87, 2592, 4896, 4627)]
    public void Sizes_EachParameterSet_MatchFips204Table2(MlDsaParameterSet parameterSet, int publicKeySize, int privateKeySize, int signatureSize)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "FIPS 204 table 2");
        diagnostics.Arrange("parameter set", parameterSet);

        string sizes = $"{MlDsa.GetPublicKeySize(parameterSet)}, {MlDsa.GetPrivateKeySize(parameterSet)}, {MlDsa.GetSignatureSize(parameterSet)}";
        diagnostics.Act("public key, private key and signature sizes", sizes);

        diagnostics.Assert("public key, private key and signature sizes", $"{publicKeySize}, {privateKeySize}, {signatureSize}", sizes);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] publicKey = new byte[MlDsa.GetPublicKeySize(parameterSet)];
        byte[] signature = new byte[MlDsa.GetSignatureSize(parameterSet)];
        byte[] message = "ML-DSA round trip"u8.ToArray();
        byte[] context = "curl"u8.ToArray();
        using MlDsa key = MlDsa.GenerateKey(parameterSet);
        key.ExportPublicKey(publicKey);
        diagnostics.Arrange("parameter set", parameterSet);
        diagnostics.Arrange("key", "random");
        diagnostics.Bytes("message", message);
        diagnostics.Bytes("context", context);

        key.SignData(message, context, signature);
        bool verified = MlDsa.VerifyData(parameterSet, publicKey, message, context, signature);
        bool verifiedWithOtherContext = MlDsa.VerifyData(parameterSet, publicKey, message, "curL"u8, signature);
        diagnostics.Act("verified with context curl, and curL", $"{verified}, {verifiedWithOtherContext}");

        diagnostics.Assert("verified with context curl, and curL", "True, False", $"{verified}, {verifiedWithOtherContext}");
        Assert.IsTrue(MlDsa.VerifyData(parameterSet, publicKey, message, context, signature));
        Assert.IsFalse(MlDsa.VerifyData(parameterSet, publicKey, message, "curL"u8, signature));
    }

    [TestMethod]
    public void SignData_TwiceWithFreshRandomness_GivesDifferentSignaturesThatBothVerify()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] publicKey = new byte[1312];
        byte[] first = new byte[2420];
        byte[] second = new byte[2420];
        using MlDsa key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, new byte[32]);
        key.ExportPublicKey(publicKey);
        diagnostics.Arrange("parameter set", MlDsaParameterSet.MlDsa44);
        diagnostics.Arrange("seed", "32 zero bytes");
        diagnostics.Arrange("message and context", "\"m\", empty");

        key.SignData("m"u8, [], first);
        key.SignData("m"u8, [], second);
        diagnostics.Act("signatures equal", first.AsSpan().SequenceEqual(second));

        diagnostics.Diff("second signature against the first (expected to differ)", first, second);
        diagnostics.Assert("both verify", "True, True", $"{MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, publicKey, "m"u8, [], first)}, {MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, publicKey, "m"u8, [], second)}");
        CollectionAssert.AreNotEqual(first, second);
        Assert.IsTrue(MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, publicKey, "m"u8, [], first));
        Assert.IsTrue(MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, publicKey, "m"u8, [], second));
    }

    [TestMethod]
    public void SignDataDeterministic_Twice_GivesTheSameSignature()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] first = new byte[2420];
        byte[] second = new byte[2420];
        using MlDsa key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, new byte[32]);
        diagnostics.Arrange("parameter set", MlDsaParameterSet.MlDsa44);
        diagnostics.Arrange("seed", "32 zero bytes");
        diagnostics.Arrange("message and context", "\"m\", \"c\"");

        key.SignDataDeterministic("m"u8, "c"u8, first);
        key.SignDataDeterministic("m"u8, "c"u8, second);
        diagnostics.Act("signatures equal", first.AsSpan().SequenceEqual(second));

        diagnostics.Diff("second signature", first, second);
        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void VerifyData_ZOutOfRange_ReturnsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors["sigVer 11"];
        byte[] signature = Hex(vector, "signature");
        Array.Clear(signature, 32, 8); // z packed as gamma1 - z: zero bytes are z = gamma1
        ArrangeVector(diagnostics, "sigVer 11", MlDsaParameterSet.MlDsa44);
        diagnostics.Arrange("tampering", "signature bytes 32 to 39 zeroed, so z = gamma1");

        bool verified = MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, Hex(vector, "pk"), Hex(vector, "message"), Hex(vector, "context"), signature);
        diagnostics.Act("verified", verified);

        diagnostics.Assert("verified", false, verified);
        Assert.IsFalse(verified);
    }

    [TestMethod]
    public void VerifyData_HintCountPastOmega_ReturnsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string> vector = Vectors["sigVer 11"];
        byte[] signature = Hex(vector, "signature");
        signature[^1] = 81; // omega is 80 for ML-DSA-44
        ArrangeVector(diagnostics, "sigVer 11", MlDsaParameterSet.MlDsa44);
        diagnostics.Arrange("tampering", "last hint count set to 81, past omega 80");

        bool verified = MlDsa.VerifyData(MlDsaParameterSet.MlDsa44, Hex(vector, "pk"), Hex(vector, "message"), Hex(vector, "context"), signature);
        diagnostics.Act("verified", verified);

        diagnostics.Assert("verified", false, verified);
        Assert.IsFalse(verified);
    }

    [TestMethod]
    public void ImportPrivateKey_TrOrT0Altered_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] privateKey = Hex(Vectors["keyGen 1"], "sk");
        byte[] trAltered = (byte[])privateKey.Clone();
        byte[] t0Altered = (byte[])privateKey.Clone();
        trAltered[64] ^= 0x01;
        t0Altered[^1] ^= 0x01;
        ArrangeVector(diagnostics, "keyGen 1", MlDsaParameterSet.MlDsa44);
        diagnostics.Arrange("tampering", "bit 0 of byte 64 (tr), or of the last byte (t0)");

        diagnostics.Act("tr altered", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.ImportPrivateKey(MlDsaParameterSet.MlDsa44, trAltered)).GetType().Name);
        diagnostics.Act("t0 altered", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.ImportPrivateKey(MlDsaParameterSet.MlDsa44, t0Altered)).GetType().Name);

        diagnostics.Assert("imports that threw ArgumentException", 2, 2);
    }

    [TestMethod]
    public void EveryMember_UndefinedParameterSet_ThrowsArgumentOutOfRangeException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const MlDsaParameterSet undefined = (MlDsaParameterSet)3;
        diagnostics.Arrange("parameter set", (int)undefined);

        diagnostics.Act("GetPublicKeySize", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.GetPublicKeySize(undefined)).GetType().Name);
        diagnostics.Act("GenerateKey", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.GenerateKey(undefined)).GetType().Name);
        diagnostics.Act("ImportPrivateKey", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.ImportPrivateKey(undefined, [])).GetType().Name);
        diagnostics.Act("VerifyData", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlDsa.VerifyData(undefined, [], [], [], [])).GetType().Name);

        diagnostics.Assert("calls that threw ArgumentOutOfRangeException", 4, 4);
    }

    [TestMethod]
    public void EveryMember_WrongLengthOrContextTooLong_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const MlDsaParameterSet set = MlDsaParameterSet.MlDsa44;
        byte[] publicKey = new byte[1312];
        byte[] signature = new byte[2420];
        byte[] longContext = new byte[256];
        using MlDsa key = MlDsa.GenerateKey(set, new byte[32]);
        diagnostics.Arrange("parameter set", set);
        diagnostics.Arrange("right lengths", "seed 32, pk 1312, sk 2560, signature 2420, rnd 32, context at most 255");

        diagnostics.Act("GenerateKey, 31-byte seed", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.GenerateKey(set, new byte[31])).GetType().Name);
        diagnostics.Act("ImportPrivateKey, 2559 bytes", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.ImportPrivateKey(set, new byte[2559])).GetType().Name);
        diagnostics.Act("VerifyData, 1311-byte public key", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.VerifyData(set, new byte[1311], [], [], signature)).GetType().Name);
        diagnostics.Act("VerifyData, 2421-byte signature", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.VerifyData(set, publicKey, [], [], new byte[2421])).GetType().Name);
        diagnostics.Act("VerifyData, 256-byte context", Assert.ThrowsExactly<ArgumentException>(() => MlDsa.VerifyData(set, publicKey, [], longContext, signature)).GetType().Name);
        diagnostics.Act("ExportPublicKey, 1313 bytes", Assert.ThrowsExactly<ArgumentException>(() => key.ExportPublicKey(new byte[1313])).GetType().Name);
        diagnostics.Act("ExportPrivateKey, 2561 bytes", Assert.ThrowsExactly<ArgumentException>(() => key.ExportPrivateKey(new byte[2561])).GetType().Name);
        diagnostics.Act("SignData, 31-byte rnd", Assert.ThrowsExactly<ArgumentException>(() => key.SignData([], [], new byte[31], signature)).GetType().Name);
        diagnostics.Act("SignData, 2419-byte signature", Assert.ThrowsExactly<ArgumentException>(() => key.SignData([], [], new byte[2419])).GetType().Name);
        diagnostics.Act("SignDataDeterministic, 256-byte context", Assert.ThrowsExactly<ArgumentException>(() => key.SignDataDeterministic([], longContext, signature)).GetType().Name);

        diagnostics.Assert("calls that threw ArgumentException", 10, 10);
    }

    [TestMethod]
    public void Dispose_ZeroesThePrivateKeyAndRefusesEveryLaterCall()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        MlDsa key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, new byte[32]);
        byte[] held = (byte[])typeof(MlDsa).GetField("privateKey", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(key)!;
        diagnostics.Arrange("parameter set", MlDsaParameterSet.MlDsa44);
        diagnostics.Arrange("seed", "32 zero bytes");

        key.Dispose();
        diagnostics.Act("held private key all zero", held.All(value => value == 0));

        diagnostics.Assert("held private key all zero", true, held.All(value => value == 0));
        Assert.IsTrue(held.All(value => value == 0));
        diagnostics.Act("ExportPublicKey", Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportPublicKey(new byte[1312])).GetType().Name);
        diagnostics.Act("ExportPrivateKey", Assert.ThrowsExactly<ObjectDisposedException>(() => key.ExportPrivateKey(new byte[2560])).GetType().Name);
        diagnostics.Act("SignDataDeterministic", Assert.ThrowsExactly<ObjectDisposedException>(() => key.SignDataDeterministic([], [], new byte[2420])).GetType().Name);
    }

    private static void ArrangeVector(TestDiagnostics diagnostics, string caseName, MlDsaParameterSet parameterSet)
    {
        diagnostics.Arrange("vector source", $"NIST ACVP ML-DSA FIPS 204, KnownAnswers/ml-dsa-acvp-fips204-selected.txt case {caseName}");
        diagnostics.Arrange("parameter set", parameterSet);
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
