using System.Security.Cryptography;
using Curl.Cryptography;

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

    [TestMethod]
    [DataRow(TlsNamedGroup.MlKem512, 800, 768)]
    [DataRow(TlsNamedGroup.MlKem768, 1184, 1088)]
    [DataRow(TlsNamedGroup.MlKem1024, 1568, 1568)]
    public void AnMlKemShareMatchesOpenSslsKnownAnswer(int group, int publicKeyLength, int ciphertextLength)
    {
        MlKemAnswer answer = MlKemAnswerOf((ushort)group);
        using MlKemKeyShare share = new((ushort)group, FixedMlKem((ushort)group));

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

        Assert.IsNull(share.ComputeSharedSecret(ciphertext[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([.. ciphertext, 0]));
        Assert.ThrowsExactly<ArgumentNullException>(() => share.ComputeSharedSecret(null!));
    }

    [TestMethod]
    public void AnMlKemShareNeedsAPureMlKemGroupAndAKeyOfItsParameterSet()
    {
        using MlKem mlKem512 = MlKem.GenerateKey(MlKemParameterSet.MlKem512);

        Assert.ThrowsExactly<ArgumentException>(() => new MlKemKeyShare(TlsNamedGroup.MlKem768, mlKem512));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MlKemKeyShare(TlsNamedGroup.MlKem768, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new MlKemKeyShare(TlsNamedGroup.X25519MlKem768, mlKem512));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MlKemKeyShare.Generate(TlsNamedGroup.SecP256r1MlKem768));
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

        Assert.AreEqual(group, share.Entry.Group);
        Assert.AreEqual(publicKeyLength, share.PublicKey.Length);
        Assert.AreEqual(ecdhAnswer.ClientPublicKey, Convert.ToHexStringLower(share.PublicKey[..pointLength]));
        Assert.AreEqual(mlKemAnswer.EncapsulationKeySha256, Convert.ToHexStringLower(SHA256.HashData(share.PublicKey[pointLength..])));
        byte[] serverShare = [.. Hex(ecdhAnswer.ServerPublicKey), .. Hex(mlKemAnswer.Ciphertext)];
        Assert.AreEqual(ecdhAnswer.SharedSecret + mlKemAnswer.SharedSecret, Convert.ToHexStringLower(share.ComputeSharedSecret(serverShare)!));
    }

    [TestMethod]
    public void AnEcdhMlKemShareRefusesAWrongLengthOrAPointOffTheCurve()
    {
        using EcdhMlKemKeyShare share = EcdhMlKemKeyShare.Generate(TlsNamedGroup.SecP256r1MlKem768);
        byte[] serverShare = [.. Hex(KeyShareKnownAnswers.Secp256r1.ServerPublicKey), .. Hex(KeyShareKnownAnswers.MlKem768.Ciphertext)];
        byte[] offCurve = [.. serverShare];
        offCurve[64] ^= 0x01;

        Assert.IsNotNull(share.ComputeSharedSecret(serverShare));
        Assert.IsNull(share.ComputeSharedSecret(serverShare[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([.. serverShare, 0]));
        Assert.IsNull(share.ComputeSharedSecret(offCurve));
        Assert.ThrowsExactly<ArgumentNullException>(() => share.ComputeSharedSecret(null!));
    }

    [TestMethod]
    public void AnEcdhMlKemShareNeedsAHybridGroupWithItsCurveAndParameterSet()
    {
        using MlKem mlKem768 = MlKem.GenerateKey(MlKemParameterSet.MlKem768);
        using EcdhKeyShare p256 = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        using EcdhKeyShare p384 = EcdhKeyShare.Generate(TlsNamedGroup.Secp384r1);

        Assert.ThrowsExactly<ArgumentException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP256r1MlKem768, p384, mlKem768));
        Assert.ThrowsExactly<ArgumentException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP384r1MlKem1024, p384, mlKem768));
        Assert.ThrowsExactly<ArgumentNullException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP256r1MlKem768, null!, mlKem768));
        Assert.ThrowsExactly<ArgumentNullException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.SecP256r1MlKem768, p256, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new EcdhMlKemKeyShare(TlsNamedGroup.X25519MlKem768, p256, mlKem768));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EcdhMlKemKeyShare.Generate(TlsNamedGroup.MlKem768));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.BrainpoolP256r1Tls13, TlsNamedGroup.BrainpoolP256r1)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1Tls13, TlsNamedGroup.BrainpoolP384r1)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1Tls13, TlsNamedGroup.BrainpoolP512r1)]
    public void ABrainpoolTls13ShareMatchesOpenSslsKnownAnswerOnItsCurve(int group, int tls12Group)
    {
        EcdhAnswer answer = EcdhAnswerOf((ushort)tls12Group);
        using BrainpoolKeyShare share = new((ushort)group, Hex(answer.ClientPrivateKey));

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
        using Tls13KeyShare share = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);

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
}
