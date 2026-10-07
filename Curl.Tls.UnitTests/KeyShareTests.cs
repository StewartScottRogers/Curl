using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The key shares on each group family: agreement between two shares, and the malformed
/// or degenerate peer values each one refuses with <see langword="null" />.
/// </summary>
[TestClass]
public sealed class KeyShareTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("group", Group(group));
        IDisposable keyGeneration = Diagnostics.Phase("key generation");
        using Tls13KeyShare client = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);
        using Tls13KeyShare server = SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group);
        keyGeneration.Dispose();
        Diagnostics.Arrange("public key lengths", $"client {client.PublicKey.Length}, server {server.PublicKey.Length}");

        byte[] clientSecret;
        byte[]? serverSecret;
        using (Diagnostics.Phase("agreement"))
        {
            clientSecret = client.ComputeSharedSecret(server.PublicKey)!;
            serverSecret = server.ComputeSharedSecret(client.PublicKey);
        }

        Diagnostics.Act("client secret length", clientSecret.Length);
        Diagnostics.Assert("client group", Group(group), Group(client.Group));
        Diagnostics.Assert("entry group", Group(group), Group(client.Entry.Group));
        Diagnostics.Diff("server secret against client secret", clientSecret, serverSecret ?? []);
        Assert.AreEqual(group, client.Group);
        Assert.AreEqual(group, client.Entry.Group);
        CollectionAssert.AreEqual(clientSecret, server.ComputeSharedSecret(client.PublicKey));
    }

    [TestMethod]
    public void AnX25519ShareRefusesAWrongLengthOrDegeneratePeer()
    {
        using X25519KeyShare share = new(RandomNumberGenerator.GetBytes(32));
        Diagnostics.Arrange("peers", "31 zero bytes (wrong length), 32 zero bytes (degenerate)");

        WriteRefused("secret from a 31-byte peer", share.ComputeSharedSecret(new byte[31]));
        WriteRefused("secret from the all-zero peer", share.ComputeSharedSecret(new byte[32]));
        Assert.IsNull(share.ComputeSharedSecret(new byte[31]));
        Assert.IsNull(share.ComputeSharedSecret(new byte[32]));
    }

    [TestMethod]
    public void AnX448ShareRefusesAWrongLengthOrDegeneratePeer()
    {
        using X448KeyShare share = new(RandomNumberGenerator.GetBytes(56));
        Diagnostics.Arrange("peers", "55 zero bytes (wrong length), 56 zero bytes (degenerate)");

        Diagnostics.Assert("public key length", 56, share.PublicKey.Length);
        WriteRefused("secret from a 55-byte peer", share.ComputeSharedSecret(new byte[55]));
        WriteRefused("secret from the all-zero peer", share.ComputeSharedSecret(new byte[56]));
        Assert.AreEqual(56, share.PublicKey.Length);
        Assert.IsNull(share.ComputeSharedSecret(new byte[55]));
        Assert.IsNull(share.ComputeSharedSecret(new byte[56]));
    }

    [TestMethod]
    public void AnX25519MlKem768ShareAgreesWithTheServersEncapsulation()
    {
        using Tls13KeyShare client = SystemTlsRandomSource.Instance.CreateKeyShare(TlsNamedGroup.X25519MlKem768);
        Diagnostics.Arrange("client public key length", client.PublicKey.Length);

        (byte[] serverShare, byte[] serverSecret) = X25519MlKem768ServerShare.Answer(client.PublicKey);

        Diagnostics.Act("server share length", serverShare.Length);
        Diagnostics.Assert("group", Group(TlsNamedGroup.X25519MlKem768), Group(client.Group));
        Diagnostics.Assert("client public key length", 1184 + 32, client.PublicKey.Length);
        Diagnostics.Assert("server share length", 1088 + 32, serverShare.Length);
        Diagnostics.Diff("client secret against the server's", serverSecret, client.ComputeSharedSecret(serverShare) ?? []);
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
        Diagnostics.Arrange("server share length", serverShare.Length);
        Diagnostics.Arrange("degenerate share", "the X25519 key after byte 1088 zeroed");

        WriteRefused("secret from a share one byte short", share.ComputeSharedSecret(serverShare[..^1]));
        WriteRefused("secret from a share one byte long", share.ComputeSharedSecret([.. serverShare, 0]));
        WriteRefused("secret from the degenerate share", share.ComputeSharedSecret(degenerate));
        Assert.IsNull(share.ComputeSharedSecret(serverShare[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([.. serverShare, 0]));
        Assert.IsNull(share.ComputeSharedSecret(degenerate));
    }

    [TestMethod]
    public void AnX25519MlKem768ShareNeedsAnMlKem768Key()
    {
        using MlKem mlKem512 = MlKem.GenerateKey(MlKemParameterSet.MlKem512);
        Diagnostics.Arrange("refused keys", "ML-KEM-512, null");

        WriteThrown("an ML-KEM-512 key", Assert.ThrowsExactly<ArgumentException>(() => new X25519MlKem768KeyShare(mlKem512, new byte[32])));
        WriteThrown("a null key", Assert.ThrowsExactly<ArgumentNullException>(() => new X25519MlKem768KeyShare(null!, new byte[32])));
    }

    [TestMethod]
    public void AnEcdhShareRefusesAWrongLengthACompressedPointAndAPointOffTheCurve()
    {
        using EcdhKeyShare share = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        byte[] compressed = [.. share.PublicKey];
        compressed[0] = 0x02;
        byte[] offCurve = [.. share.PublicKey];
        offCurve[^1] ^= 0x01;
        Diagnostics.Arrange("group", "secp256r1");
        Diagnostics.Arrange("peers", "64 zero bytes, the own point with prefix 0x02, the own point with its last byte XOR 0x01");

        WriteRefused("secret from a 64-byte peer", share.ComputeSharedSecret(new byte[64]));
        WriteRefused("secret from the compressed point", share.ComputeSharedSecret(compressed));
        WriteRefused("secret from the off-curve point", share.ComputeSharedSecret(offCurve));
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
        Diagnostics.Arrange("group", Group(group));
        Diagnostics.Arrange("coordinate length", length);

        WriteRefused("secret from X all 0xff", share.ComputeSharedSecret(bigX));
        WriteRefused("secret from Y all 0xff", share.ComputeSharedSecret(bigY));
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
        Diagnostics.Arrange("group", Group(group));
        Diagnostics.Arrange("peers", "the peer's point, it with its last byte XOR 0x01, it one byte short, the single byte 0x00");

        Diagnostics.Assert("public key length", publicKeyLength, share.PublicKey.Length);
        Diagnostics.Assert("secret length", secretLength, share.ComputeSharedSecret(peer.PublicKey)?.Length);
        WriteRefused("secret from the off-curve point", share.ComputeSharedSecret(offCurve));
        WriteRefused("secret from a point one byte short", share.ComputeSharedSecret(peer.PublicKey[..^1]));
        WriteRefused("secret from a single byte", share.ComputeSharedSecret([0]));
        Assert.AreEqual(publicKeyLength, share.PublicKey.Length);
        Assert.AreEqual(secretLength, share.ComputeSharedSecret(peer.PublicKey)!.Length);
        Assert.IsNull(share.ComputeSharedSecret(offCurve));
        Assert.IsNull(share.ComputeSharedSecret(peer.PublicKey[..^1]));
        Assert.IsNull(share.ComputeSharedSecret([0]));
    }

    [TestMethod]
    public void ABrainpoolShareIsOnlyForABrainpoolCurveAndAKeyInRange()
    {
        Diagnostics.Arrange("refused", "generating on secp256r1, X448, a null key, an all-zero key, a null peer");

        WriteThrown("generating on secp256r1", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BrainpoolKeyShare.Generate(TlsNamedGroup.Secp256r1)));
        WriteThrown("X448", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BrainpoolKeyShare(TlsNamedGroup.X448, new byte[32])));
        WriteThrown("a null key", Assert.ThrowsExactly<ArgumentNullException>(() => new BrainpoolKeyShare(TlsNamedGroup.BrainpoolP256r1, null!)));
        WriteThrown("an all-zero key", Assert.ThrowsExactly<ArgumentException>(() => new BrainpoolKeyShare(TlsNamedGroup.BrainpoolP256r1, new byte[32])));
        using BrainpoolKeyShare share = BrainpoolKeyShare.Generate(TlsNamedGroup.BrainpoolP256r1);
        WriteThrown("a null peer", Assert.ThrowsExactly<ArgumentNullException>(() => share.ComputeSharedSecret(null!)));
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
    public void IsTls12EcdheGroupNamesTheGroupsTlsOneTwoAgreesOn(int group, bool expected)
    {
        Diagnostics.Arrange("group", Group(group));

        bool actual = TlsNamedGroup.IsTls12EcdheGroup((ushort)group);

        Diagnostics.Act("is a TLS 1.2 ECDHE group", actual);
        Diagnostics.Assert("is a TLS 1.2 ECDHE group", expected, actual);
        Assert.AreEqual(expected, TlsNamedGroup.IsTls12EcdheGroup((ushort)group));
    }

    [TestMethod]
    public void AnEcdhShareIsOnlyForANistCurve()
    {
        Diagnostics.Arrange("group", "X25519");

        WriteThrown("generating on X25519", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EcdhKeyShare.Generate(TlsNamedGroup.X25519)));
    }

    [TestMethod]
    public void AFiniteFieldShareRefusesAWrongLengthOrOutOfRangePeer()
    {
        using Tls13KeyShare share = SystemTlsRandomSource.Instance.CreateKeyShare(TlsNamedGroup.Ffdhe2048);
        byte[] one = new byte[256];
        one[^1] = 1;
        Diagnostics.Arrange("group", "ffdhe2048");
        Diagnostics.Arrange("peers", "255 zero bytes (wrong length), the value 1 in 256 bytes (out of range)");

        WriteRefused("secret from a 255-byte peer", share.ComputeSharedSecret(new byte[255]));
        WriteRefused("secret from the peer value 1", share.ComputeSharedSecret(one));
        Assert.IsNull(share.ComputeSharedSecret(new byte[255]));
        Assert.IsNull(share.ComputeSharedSecret(one));
    }

    [TestMethod]
    public void AFiniteFieldShareIsOnlyForAFiniteFieldGroup()
    {
        Diagnostics.Arrange("group", "secp256r1");

        WriteThrown("a finite-field share on secp256r1", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new FfdheKeyShare(TlsNamedGroup.Secp256r1, [1])));
    }

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
    public void CanShareNamesTheGroupsWithAKeyShare(int group, bool expected)
    {
        Diagnostics.Arrange("group", Group(group));

        bool actual = TlsNamedGroup.CanShare((ushort)group);

        Diagnostics.Act("can share", actual);
        Diagnostics.Assert("can share", expected, actual);
        Assert.AreEqual(expected, TlsNamedGroup.CanShare((ushort)group));
    }

    [TestMethod]
    [DataRow(0x0016)]
    [DataRow(0x0022)]
    [DataRow(0x0105)]
    [DataRow(0x0203)]
    [DataRow(0x11ee)]
    public void TheSystemRandomSourceRefusesAGroupItCannotShare(int group)
    {
        Diagnostics.Arrange("group", Group(group));

        WriteThrown("creating a key share", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SystemTlsRandomSource.Instance.CreateKeyShare((ushort)group)));
    }

    [TestMethod]
    public void TheSystemRandomSourceFillsBytes()
    {
        byte[] bytes = new byte[64];
        Diagnostics.Arrange("buffer", "64 zero bytes");

        SystemTlsRandomSource.Instance.Fill(bytes);

        Diagnostics.Act("non-zero bytes", bytes.Count(value => value != 0));
        Diagnostics.Assert("any byte non-zero", true, bytes.Any(value => value != 0));
        Assert.IsTrue(bytes.Any(value => value != 0));
    }

    private static string Group(int group) => $"0x{group:x4}";

    private void WriteRefused(string label, byte[]? secret)
    {
        string actual = secret is null ? "null" : Convert.ToHexStringLower(secret);
        Diagnostics.Act(label, actual);
        Diagnostics.Assert(label, "null", actual);
    }

    private void WriteThrown(string input, Exception thrown)
    {
        Diagnostics.Act($"{input} threw", $"{thrown.GetType().Name}: {thrown.Message}");
        Diagnostics.Assert($"{input} exception", thrown.GetType().Name, thrown.GetType().Name);
    }
}
