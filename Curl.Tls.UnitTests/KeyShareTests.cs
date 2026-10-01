using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The key shares on each group family: agreement between two shares, and the malformed
/// or degenerate peer values each one refuses with <see langword="null" />.
/// </summary>
[TestClass]
public sealed class KeyShareTests
{
    [TestMethod]
    [DataRow(TlsNamedGroup.X25519)]
    [DataRow(TlsNamedGroup.X448)]
    [DataRow(TlsNamedGroup.Secp256r1)]
    [DataRow(TlsNamedGroup.Secp384r1)]
    [DataRow(TlsNamedGroup.Secp521r1)]
    [DataRow(TlsNamedGroup.Ffdhe2048)]
    [DataRow(TlsNamedGroup.Ffdhe3072)]
    [DataRow(TlsNamedGroup.Ffdhe4096)]
    [DataRow(TlsNamedGroup.Ffdhe6144)]
    [DataRow(TlsNamedGroup.Ffdhe8192)]
    [DataRow(TlsNamedGroup.BrainpoolP256r1)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1)]
    public void TwoSharesOnAGroupAgreeOnTheSharedSecret(int group)
    {
        using Tls13KeyShare client = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);
        using Tls13KeyShare server = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);

        byte[] clientSecret = client.ComputeSharedSecret(server.PublicKey)!;

        Assert.AreEqual(group, client.Group);
        Assert.AreEqual(group, client.Entry.Group);
        CollectionAssert.AreEqual(clientSecret, server.ComputeSharedSecret(client.PublicKey));
    }

    [TestMethod]
    public void AnX25519ShareRefusesAWrongLengthOrDegeneratePeer()
    {
        using X25519KeyShare share = new(RandomNumberGenerator.GetBytes(32));

        Assert.IsNull(share.ComputeSharedSecret(new byte[31]));
        Assert.IsNull(share.ComputeSharedSecret(new byte[32]));
    }

    [TestMethod]
    public void AnX448ShareRefusesAWrongLengthOrDegeneratePeer()
    {
        using X448KeyShare share = new(RandomNumberGenerator.GetBytes(56));

        Assert.AreEqual(56, share.PublicKey.Length);
        Assert.IsNull(share.ComputeSharedSecret(new byte[55]));
        Assert.IsNull(share.ComputeSharedSecret(new byte[56]));
    }

    [TestMethod]
    public void AnX25519MlKem768ShareAgreesWithTheServersEncapsulation()
    {
        using Tls13KeyShare client = SystemTlsRandomSource.Instance.CreateKeyShare(TlsNamedGroup.X25519MlKem768);

        (byte[] serverShare, byte[] serverSecret) = X25519MlKem768ServerShare.Answer(client.PublicKey);

        Assert.AreEqual(TlsNamedGroup.X25519MlKem768, client.Group);
        Assert.AreEqual(1184 + 32, client.PublicKey.Length);
        Assert.AreEqual(1088 + 32, X25519MlKem768KeyShare.ServerShareLength);
        Assert.AreEqual(1088 + 32, serverShare.Length);
        CollectionAssert.AreEqual(serverSecret, client.ComputeSharedSecret(serverShare));
    }

    [TestMethod]
    public void AnX25519MlKem768ShareRefusesAWrongLengthOrADegenerateX25519Key()
    {
        using X25519MlKem768KeyShare share = X25519MlKem768KeyShare.Generate();
        (byte[] serverShare, _) = X25519MlKem768ServerShare.Answer(share.PublicKey);
        byte[] degenerate = [.. serverShare];
        degenerate.AsSpan(1088).Clear();

        Assert.IsNull(share.ComputeSharedSecret(serverShare[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([.. serverShare, 0]));
        Assert.IsNull(share.ComputeSharedSecret(degenerate));
    }

    [TestMethod]
    public void AnX25519MlKem768ShareNeedsAnMlKem768Key()
    {
        using MlKem mlKem512 = MlKem.GenerateKey(MlKemParameterSet.MlKem512);

        Assert.ThrowsExactly<ArgumentException>(() => new X25519MlKem768KeyShare(mlKem512, new byte[32]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new X25519MlKem768KeyShare(null!, new byte[32]));
    }

    [TestMethod]
    public void AnEcdhShareRefusesAWrongLengthACompressedPointAndAPointOffTheCurve()
    {
        using EcdhKeyShare share = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        byte[] compressed = [.. share.PublicKey];
        compressed[0] = 0x02;
        byte[] offCurve = [.. share.PublicKey];
        offCurve[^1] ^= 0x01;

        Assert.IsNull(share.ComputeSharedSecret(new byte[64]));
        Assert.IsNull(share.ComputeSharedSecret(compressed));
        Assert.IsNull(share.ComputeSharedSecret(offCurve));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.Secp256r1)]
    [DataRow(TlsNamedGroup.Secp384r1)]
    [DataRow(TlsNamedGroup.Secp521r1)]
    public void AnEcdhShareRefusesACoordinateNotBelowThePrime(int group)
    {
        using EcdhKeyShare share = EcdhKeyShare.Generate((ushort)group);
        int length = (share.PublicKey.Length - 1) / 2;
        byte[] bigX = [.. share.PublicKey];
        bigX.AsSpan(1, length).Fill(0xff);
        byte[] bigY = [.. share.PublicKey];
        bigY.AsSpan(1 + length).Fill(0xff);

        Assert.IsNull(share.ComputeSharedSecret(bigX));
        Assert.IsNull(share.ComputeSharedSecret(bigY));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.BrainpoolP256r1, 65, 32)]
    [DataRow(TlsNamedGroup.BrainpoolP384r1, 97, 48)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1, 129, 64)]
    public void ABrainpoolShareRefusesAPeerThatIsNotAPointOfItsCurve(int group, int publicKeyLength, int secretLength)
    {
        using Tls13KeyShare share = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);
        using Tls13KeyShare peer = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);
        byte[] offCurve = [.. peer.PublicKey];
        offCurve[^1] ^= 0x01;

        Assert.AreEqual(publicKeyLength, share.PublicKey.Length);
        Assert.AreEqual(secretLength, share.ComputeSharedSecret(peer.PublicKey)!.Length);
        Assert.IsNull(share.ComputeSharedSecret(offCurve));
        Assert.IsNull(share.ComputeSharedSecret(peer.PublicKey[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([0]));
    }

    [TestMethod]
    public void ABrainpoolShareIsOnlyForABrainpoolCurveAndAKeyInRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BrainpoolKeyShare.Generate(TlsNamedGroup.Secp256r1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BrainpoolKeyShare(TlsNamedGroup.X448, new byte[32]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BrainpoolKeyShare(TlsNamedGroup.BrainpoolP256r1, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new BrainpoolKeyShare(TlsNamedGroup.BrainpoolP256r1, new byte[32]));
        using BrainpoolKeyShare share = BrainpoolKeyShare.Generate(TlsNamedGroup.BrainpoolP256r1);
        Assert.ThrowsExactly<ArgumentNullException>(() => share.ComputeSharedSecret(null!));
    }

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519, true)]
    [DataRow(TlsNamedGroup.X448, true)]
    [DataRow(TlsNamedGroup.Secp256r1, true)]
    [DataRow(TlsNamedGroup.Secp521r1, true)]
    [DataRow(TlsNamedGroup.BrainpoolP256r1, true)]
    [DataRow(TlsNamedGroup.BrainpoolP512r1, true)]
    [DataRow(0x0016, false)]
    [DataRow(0x001f, false)]
    [DataRow(TlsNamedGroup.Ffdhe2048, false)]
    [DataRow(TlsNamedGroup.X25519MlKem768, false)]
    public void IsTls12EcdheGroupNamesTheGroupsTlsOneTwoAgreesOn(int group, bool expected) =>
        Assert.AreEqual(expected, TlsNamedGroup.IsTls12EcdheGroup((ushort)group));

    [TestMethod]
    public void AnEcdhShareIsOnlyForANistCurve() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EcdhKeyShare.Generate(TlsNamedGroup.X25519));

    [TestMethod]
    public void AFiniteFieldShareRefusesAWrongLengthOrOutOfRangePeer()
    {
        using Tls13KeyShare share = SystemTlsRandomSource.Instance.CreateKeyShare(TlsNamedGroup.Ffdhe2048);
        byte[] one = new byte[256];
        one[^1] = 1;

        Assert.IsNull(share.ComputeSharedSecret(new byte[255]));
        Assert.IsNull(share.ComputeSharedSecret(one));
    }

    [TestMethod]
    public void AFiniteFieldShareIsOnlyForAFiniteFieldGroup() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FfdheKeyShare(TlsNamedGroup.Secp256r1, [1]));

    [TestMethod]
    [DataRow(TlsNamedGroup.X25519, true)]
    [DataRow(TlsNamedGroup.Secp521r1, true)]
    [DataRow(TlsNamedGroup.Ffdhe2048, true)]
    [DataRow(TlsNamedGroup.Ffdhe8192, true)]
    [DataRow(TlsNamedGroup.X448, true)]
    [DataRow(TlsNamedGroup.X25519MlKem768, true)]
    [DataRow(0x0016, false)]
    [DataRow(0x00ff, false)]
    [DataRow(0x0105, false)]
    [DataRow(0x11eb, true)]
    [DataRow(0x0022, false)]
    [DataRow(0x0203, false)]
    [DataRow(0x11ee, false)]
    public void CanShareNamesTheGroupsWithAKeyShare(int group, bool expected) =>
        Assert.AreEqual(expected, TlsNamedGroup.CanShare((ushort)group));

    [TestMethod]
    [DataRow(0x0016)]
    [DataRow(0x0022)]
    [DataRow(0x0105)]
    [DataRow(0x0203)]
    [DataRow(0x11ee)]
    public void TheSystemRandomSourceRefusesAGroupItCannotShare(int group) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group));

    [TestMethod]
    public void TheSystemRandomSourceFillsBytes()
    {
        byte[] bytes = new byte[64];

        SystemTlsRandomSource.Instance.Fill(bytes);

        Assert.IsTrue(bytes.Any(value => value != 0));
    }
}
