using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The pure ML-KEM, ML-KEM hybrid and brainpool <c>tls13</c> key shares (BL-1049) against
/// OpenSSL's known answers (<see cref="KeyShareKnownAnswers" />): the public value each puts
/// in its <c>key_share</c> entry, the shared secret each takes from the server's answer, and
/// the malformed answers and mismatched keys each refuses.
/// </summary>
[TestClass]
public sealed class KeyShareKnownAnswerTests
{
    private static readonly byte[] SeedD = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    private static readonly byte[] SeedZ = [.. Enumerable.Range(32, 32).Select(value => (byte)value)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsNamedGroup.MlKem512, 800, 768)]
    [DataRow(TlsNamedGroup.MlKem768, 1184, 1088)]
    [DataRow(TlsNamedGroup.MlKem1024, 1568, 1568)]
    public void AnMlKemShareMatchesOpenSslsKnownAnswer(int group, int publicKeyLength, int ciphertextLength)
    {
        MlKemAnswer answer = MlKemAnswerOf((ushort)group);
        Diagnostics.Arrange("group", $"0x{group:x4}");
        Diagnostics.Arrange("expected encapsulation key SHA-256", answer.EncapsulationKeySha256);
        Diagnostics.Bytes("server ciphertext", Hex(answer.Ciphertext));
        using MlKemKeyShare share = new((ushort)group, FixedMlKem((ushort)group));
        string sharedSecret = Convert.ToHexStringLower(share.ComputeSharedSecret(Hex(answer.Ciphertext))!);

        Diagnostics.Act("public key length", share.PublicKey.Length);
        Diagnostics.Act("shared secret", sharedSecret);
        Diagnostics.Assert("entry group", group, share.Entry.Group);
        Diagnostics.Assert("public key length", publicKeyLength, share.PublicKey.Length);
        Diagnostics.Diff("encapsulation key SHA-256", answer.EncapsulationKeySha256, Convert.ToHexStringLower(SHA256.HashData(share.PublicKey)));
        Diagnostics.Diff("shared secret", answer.SharedSecret, sharedSecret);
        Assert.AreEqual(group, share.Entry.Group);
        Assert.AreEqual(publicKeyLength, share.PublicKey.Length);
        Assert.AreEqual(answer.EncapsulationKeySha256, Convert.ToHexStringLower(SHA256.HashData(share.PublicKey)));
        Assert.AreEqual(ciphertextLength, Hex(answer.Ciphertext).Length);
        Assert.AreEqual(answer.SharedSecret, Convert.ToHexStringLower(share.ComputeSharedSecret(Hex(answer.Ciphertext))!));
    }

    [TestMethod]
    public void AnMlKemShareRefusesACiphertextOfTheWrongLength()
    {
        using MlKemKeyShare share = MlKemKeyShare.Generate(TlsNamedGroup.MlKem768);
        byte[] ciphertext = Hex(KeyShareKnownAnswers.MlKem768.Ciphertext);
        Diagnostics.Arrange("group", "ML-KEM-768");
        Diagnostics.Arrange("ciphertext lengths tried", $"{ciphertext.Length - 1}, {ciphertext.Length + 1} (expected {ciphertext.Length})");

        string shortSecret = SecretText(share.ComputeSharedSecret(ciphertext[..^1]));
        string longSecret = SecretText(share.ComputeSharedSecret([.. ciphertext, 0]));

        Diagnostics.Act("secret from a ciphertext one byte short", shortSecret);
        Diagnostics.Act("secret from a ciphertext one byte long", longSecret);
        Diagnostics.Assert("secret from a ciphertext one byte short", "null", shortSecret);
        Diagnostics.Assert("secret from a ciphertext one byte long", "null", longSecret);
        Assert.IsNull(share.ComputeSharedSecret(ciphertext[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([.. ciphertext, 0]));
        ArgumentNullException nullCiphertext = Assert.ThrowsExactly<ArgumentNullException>(() => share.ComputeSharedSecret(null!));
        WriteThrown("null ciphertext", nullCiphertext);
    }

    [TestMethod]
    public void AnMlKemShareNeedsAPureMlKemGroupAndAKeyOfItsParameterSet()
    {
        using MlKem mlKem512 = MlKem.GenerateKey(MlKemParameterSet.MlKem512);
        Diagnostics.Arrange("key", "ML-KEM-512");
        Diagnostics.Arrange("refused", "ML-KEM-768 with an ML-KEM-512 key, a null key, X25519MLKEM768, generating SecP256r1MLKEM768");

        WriteThrown("ML-KEM-768 group with an ML-KEM-512 key", Assert.ThrowsExactly<ArgumentException>(() => new MlKemKeyShare(TlsNamedGroup.MlKem768, mlKem512)));
        WriteThrown("null key", Assert.ThrowsExactly<ArgumentNullException>(() => new MlKemKeyShare(TlsNamedGroup.MlKem768, null!)));
        WriteThrown("hybrid group X25519MLKEM768", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MlKemKeyShare(TlsNamedGroup.X25519MlKem768, mlKem512)));
        WriteThrown("generating SecP256r1MLKEM768", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKemKeyShare.Generate(TlsNamedGroup.SecP256r1MlKem768)));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.SecP256r1MlKem768, TlsNamedGroup.Secp256r1, TlsNamedGroup.MlKem768, 65 + 1184)]
    [DataRow(TlsNamedGroup.SecP384r1MlKem1024, TlsNamedGroup.Secp384r1, TlsNamedGroup.MlKem1024, 97 + 1568)]
    public void AnEcdhMlKemShareMatchesOpenSslsKnownAnswersWithTheCurveFirst(int group, int curve, int mlKemGroup, int publicKeyLength)
    {
        EcdhAnswer ecdhAnswer = EcdhAnswerOf((ushort)curve);
        MlKemAnswer mlKemAnswer = MlKemAnswerOf((ushort)mlKemGroup);
        using EcdhMlKemKeyShare share = new((ushort)group, FixedEcdh((ushort)curve), FixedMlKem((ushort)mlKemGroup));
        int pointLength = Hex(ecdhAnswer.ClientPublicKey).Length;
        Diagnostics.Arrange("group", $"0x{group:x4} (curve 0x{curve:x4}, ML-KEM 0x{mlKemGroup:x4})");
        Diagnostics.Arrange("curve point length", pointLength);
        Diagnostics.Act("public key length", share.PublicKey.Length);
        Diagnostics.Bytes("public key", share.PublicKey);

        Diagnostics.Assert("entry group", group, share.Entry.Group);
        Diagnostics.Assert("public key length", publicKeyLength, share.PublicKey.Length);
        Diagnostics.Diff("curve point", ecdhAnswer.ClientPublicKey, Convert.ToHexStringLower(share.PublicKey[..pointLength]));
        Assert.AreEqual(group, share.Entry.Group);
        Assert.AreEqual(publicKeyLength, share.PublicKey.Length);
        Assert.AreEqual(ecdhAnswer.ClientPublicKey, Convert.ToHexStringLower(share.PublicKey[..pointLength]));
        Assert.AreEqual(mlKemAnswer.EncapsulationKeySha256, Convert.ToHexStringLower(SHA256.HashData(share.PublicKey[pointLength..])));
        byte[] serverShare = [.. Hex(ecdhAnswer.ServerPublicKey), .. Hex(mlKemAnswer.Ciphertext)];
        Diagnostics.Bytes("server share", serverShare);
        Diagnostics.Diff("shared secret", ecdhAnswer.SharedSecret + mlKemAnswer.SharedSecret, SecretText(share.ComputeSharedSecret(serverShare)));
        Assert.AreEqual(ecdhAnswer.SharedSecret + mlKemAnswer.SharedSecret, Convert.ToHexStringLower(share.ComputeSharedSecret(serverShare)!));
    }

    [TestMethod]
    public void AnEcdhMlKemShareRefusesAWrongLengthOrAPointOffTheCurve()
    {
        using EcdhMlKemKeyShare share = EcdhMlKemKeyShare.Generate(TlsNamedGroup.SecP256r1MlKem768);
        byte[] serverShare = [.. Hex(KeyShareKnownAnswers.Secp256r1.ServerPublicKey), .. Hex(KeyShareKnownAnswers.MlKem768.Ciphertext)];
        byte[] offCurve = [.. serverShare];
        offCurve[64] ^= 0x01;
        Diagnostics.Arrange("group", "SecP256r1MLKEM768");
        Diagnostics.Bytes("server share", serverShare);
        Diagnostics.Arrange("off-curve share", "byte 64 (the point's last Y byte) XOR 0x01");

        Diagnostics.Act("secret from the good share", SecretText(share.ComputeSharedSecret(serverShare)));
        Diagnostics.Assert("secret from a share one byte short", "null", SecretText(share.ComputeSharedSecret(serverShare[..^1])));
        Diagnostics.Assert("secret from a share one byte long", "null", SecretText(share.ComputeSharedSecret([.. serverShare, 0])));
        Diagnostics.Assert("secret from the off-curve share", "null", SecretText(share.ComputeSharedSecret(offCurve)));
        Assert.IsNotNull(share.ComputeSharedSecret(serverShare));
        Assert.IsNull(share.ComputeSharedSecret(serverShare[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([.. serverShare, 0]));
        Assert.IsNull(share.ComputeSharedSecret(offCurve));
        WriteThrown("null share", Assert.ThrowsExactly<ArgumentNullException>(() => share.ComputeSharedSecret(null!)));
    }

    [TestMethod]
    public void AnEcdhMlKemShareNeedsAHybridGroupWithItsCurveAndParameterSet()
    {
        using MlKem mlKem768 = MlKem.GenerateKey(MlKemParameterSet.MlKem768);
        using EcdhKeyShare p256 = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        using EcdhKeyShare p384 = EcdhKeyShare.Generate(TlsNamedGroup.Secp384r1);
        Diagnostics.Arrange("keys", "ML-KEM-768, P-256, P-384");

        WriteThrown("SecP256r1MLKEM768 with a P-384 key", Assert.ThrowsExactly<ArgumentException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP256r1MlKem768, p384, mlKem768)));
        WriteThrown("SecP384r1MLKEM1024 with ML-KEM-768", Assert.ThrowsExactly<ArgumentException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP384r1MlKem1024, p384, mlKem768)));
        WriteThrown("null curve key", Assert.ThrowsExactly<ArgumentNullException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP256r1MlKem768, null!, mlKem768)));
        WriteThrown("null ML-KEM key", Assert.ThrowsExactly<ArgumentNullException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP256r1MlKem768, p256, null!)));
        WriteThrown("X25519MLKEM768", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.X25519MlKem768, p256, mlKem768)));
        WriteThrown("generating pure ML-KEM-768", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EcdhMlKemKeyShare.Generate(TlsNamedGroup.MlKem768)));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.BrainpoolP256r1Tls13, TlsNamedGroup.BrainpoolP256r1)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1Tls13, TlsNamedGroup.BrainpoolP384r1)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1Tls13, TlsNamedGroup.BrainpoolP512r1)]
    public void ABrainpoolTls13ShareMatchesOpenSslsKnownAnswerOnItsCurve(int group, int tls12Group)
    {
        EcdhAnswer answer = EcdhAnswerOf((ushort)tls12Group);
        using BrainpoolKeyShare share = new((ushort)group, Hex(answer.ClientPrivateKey));
        Diagnostics.Arrange("group", $"0x{group:x4} (TLS 1.2 curve 0x{tls12Group:x4})");
        Diagnostics.Arrange("server public key", answer.ServerPublicKey);
        string sharedSecret = SecretText(share.ComputeSharedSecret(Hex(answer.ServerPublicKey)));
        Diagnostics.Act("shared secret", sharedSecret);

        Diagnostics.Assert("entry group", group, share.Entry.Group);
        Diagnostics.Diff("public key", answer.ClientPublicKey, Convert.ToHexStringLower(share.PublicKey));
        Diagnostics.Diff("shared secret", answer.SharedSecret, sharedSecret);
        Assert.AreEqual(group, share.Entry.Group);
        Assert.AreEqual(answer.ClientPublicKey, Convert.ToHexStringLower(share.PublicKey));
        Assert.AreEqual(answer.SharedSecret, Convert.ToHexStringLower(share.ComputeSharedSecret(Hex(answer.ServerPublicKey))!));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.MlKem512, 800)]
    [DataRow(TlsNamedGroup.MlKem768, 1184)]
    [DataRow(TlsNamedGroup.MlKem1024, 1568)]
    [DataRow(TlsNamedGroup.SecP256r1MlKem768, 1249)]
    [DataRow(TlsNamedGroup.SecP384r1MlKem1024, 1665)]
    [DataRow(TlsNamedGroup.BrainpoolP256r1Tls13, 65)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1Tls13, 97)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1Tls13, 129)]
    public void TheSystemRandomSourceSharesEachNewGroupAtTheMeasuredLength(int group, int publicKeyLength)
    {
        Diagnostics.Arrange("group", $"0x{group:x4}");
        using Tls13KeyShare share = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);
        Diagnostics.Act("share group", $"0x{share.Group:x4}");
        Diagnostics.Act("public key length", share.PublicKey.Length);

        Diagnostics.Assert("can share", true, TlsNamedGroup.CanShare((ushort)group));
        Diagnostics.Assert("is a TLS 1.2 ECDHE group", false, TlsNamedGroup.IsTls12EcdheGroup((ushort)group));
        Diagnostics.Assert("public key length", publicKeyLength, share.PublicKey.Length);
        Assert.IsTrue(TlsNamedGroup.CanShare((ushort)group));
        Assert.IsFalse(TlsNamedGroup.IsTls12EcdheGroup((ushort)group));
        Assert.AreEqual(group, share.Group);
        Assert.AreEqual(publicKeyLength, share.PublicKey.Length);
    }

    private static MlKem FixedMlKem(ushort group) => MlKem.GenerateKey(MlKemKeyShare.ParameterSetOf(group), SeedD, SeedZ);

    private static EcdhKeyShare FixedEcdh(ushort curve)
    {
        EcdhAnswer answer = EcdhAnswerOf(curve);
        byte[] point = Hex(answer.ClientPublicKey);
        int length = point.Length / 2;
        ECCurve namedCurve = curve == TlsNamedGroup.Secp256r1 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384;
        return new EcdhKeyShare(curve, ECDiffieHellman.Create(new ECParameters
        {
            Curve = namedCurve,
            D = Hex(answer.ClientPrivateKey),
            Q = new ECPoint { X = point[1..(1 + length)], Y = point[(1 + length)..] },
        }));
    }

    private static MlKemAnswer MlKemAnswerOf(ushort group) => group switch
    {
        TlsNamedGroup.MlKem512 => KeyShareKnownAnswers.MlKem512,
        TlsNamedGroup.MlKem768 => KeyShareKnownAnswers.MlKem768,
        _ => KeyShareKnownAnswers.MlKem1024,
    };

    private static EcdhAnswer EcdhAnswerOf(ushort group) => group switch
    {
        TlsNamedGroup.Secp256r1 => KeyShareKnownAnswers.Secp256r1,
        TlsNamedGroup.Secp384r1 => KeyShareKnownAnswers.Secp384r1,
        TlsNamedGroup.BrainpoolP256r1 => KeyShareKnownAnswers.BrainpoolP256r1,
        TlsNamedGroup.BrainpoolP384r1 => KeyShareKnownAnswers.BrainpoolP384r1,
        _ => KeyShareKnownAnswers.BrainpoolP512r1,
    };

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static string SecretText(byte[]? secret) => secret is null ? "null" : Convert.ToHexStringLower(secret);

    private void WriteThrown(string input, Exception thrown)
    {
        Diagnostics.Act($"{input} threw", $"{thrown.GetType().Name}: {thrown.Message}");
        Diagnostics.Assert($"{input} exception", thrown.GetType().Name, thrown.GetType().Name);
    }
}
