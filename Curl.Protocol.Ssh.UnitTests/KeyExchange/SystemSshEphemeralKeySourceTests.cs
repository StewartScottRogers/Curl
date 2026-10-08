using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Testing;

namespace Curl.Protocol.Ssh.KeyExchange;

[TestClass]
public sealed class SystemSshEphemeralKeySourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CreateEllipticCurveKey_GivesAFreshKeyOnTheCurve()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SystemSshEphemeralKeySource source = new();
        diagnostics.Arrange("curve", "nistP384");

        using ECDiffieHellman first = source.CreateEllipticCurveKey(ECCurve.NamedCurves.nistP384);
        using ECDiffieHellman second = source.CreateEllipticCurveKey(ECCurve.NamedCurves.nistP384);

        byte[] firstX = first.ExportParameters(false).Q.X!;
        byte[] secondX = second.ExportParameters(false).Q.X!;
        diagnostics.Act("first key size", first.KeySize);
        diagnostics.Bytes("first public X", firstX);
        diagnostics.Bytes("second public X", secondX);
        diagnostics.Assert("first key size", 384, first.KeySize);
        diagnostics.Assert("public X values differ", true, !firstX.AsSpan().SequenceEqual(secondX));
        Assert.AreEqual(384, first.KeySize);
        CollectionAssert.AreNotEqual(first.ExportParameters(false).Q.X, second.ExportParameters(false).Q.X);
    }

    [TestMethod]
    public void CreateFiniteFieldKey_GivesAFreshKeyInTheGroup()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SystemSshEphemeralKeySource source = new();
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group14;
        byte[] first = new byte[group.PrimeLength];
        byte[] second = new byte[group.PrimeLength];
        diagnostics.Arrange("group", $"Group14 ({group.PrimeLength}-byte prime)");

        bool sameGroup;
        using (diagnostics.Phase("first key"))
        using (FiniteFieldDiffieHellman key = source.CreateFiniteFieldKey(group))
        {
            sameGroup = ReferenceEquals(group, key.Group);
            diagnostics.Act("key group is Group14", sameGroup);
            diagnostics.Assert("key group is Group14", true, sameGroup);
            Assert.AreSame(group, key.Group);
            key.ComputePublicValue(first);
        }

        using (diagnostics.Phase("second key"))
        using (FiniteFieldDiffieHellman key = source.CreateFiniteFieldKey(group))
        {
            key.ComputePublicValue(second);
        }

        diagnostics.Bytes("first public value", first);
        diagnostics.Bytes("second public value", second);
        diagnostics.Assert("public values differ", true, !first.AsSpan().SequenceEqual(second));
        CollectionAssert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void CreateX25519PrivateKey_FillsAFreshKey()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SystemSshEphemeralKeySource source = new();
        byte[] first = new byte[X25519.KeySize];
        byte[] second = new byte[X25519.KeySize];
        diagnostics.Arrange("key size", X25519.KeySize);

        source.CreateX25519PrivateKey(first);
        source.CreateX25519PrivateKey(second);

        diagnostics.Act("first private key", Convert.ToHexString(first));
        diagnostics.Act("second private key", Convert.ToHexString(second));
        diagnostics.Assert("first key is not all zero", true, first.Any(value => value != 0));
        diagnostics.Assert("keys differ", true, !first.AsSpan().SequenceEqual(second));
        CollectionAssert.AreNotEqual(new byte[X25519.KeySize], first);
        CollectionAssert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void CreateMlKemKey_GivesAFreshKeyOfTheParameterSet()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SystemSshEphemeralKeySource source = new();
        byte[] first = new byte[MlKem.GetEncapsulationKeySize(MlKemParameterSet.MlKem1024)];
        byte[] second = new byte[first.Length];
        diagnostics.Arrange("parameter set", $"{MlKemParameterSet.MlKem1024} ({first.Length}-byte encapsulation key)");

        using (diagnostics.Phase("first key"))
        using (MlKem key = source.CreateMlKemKey(MlKemParameterSet.MlKem1024))
        {
            diagnostics.Act("parameter set", key.ParameterSet);
            diagnostics.Assert("parameter set", MlKemParameterSet.MlKem1024, key.ParameterSet);
            Assert.AreEqual(MlKemParameterSet.MlKem1024, key.ParameterSet);
            key.ExportEncapsulationKey(first);
        }

        using (diagnostics.Phase("second key"))
        using (MlKem key = source.CreateMlKemKey(MlKemParameterSet.MlKem1024))
        {
            key.ExportEncapsulationKey(second);
        }

        diagnostics.Bytes("first encapsulation key", first);
        diagnostics.Bytes("second encapsulation key", second);
        diagnostics.Assert("encapsulation keys differ", true, !first.AsSpan().SequenceEqual(second));
        CollectionAssert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void CreateSntrup761KeyPair_FillsAFreshKeyPairThatDecapsulates()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SystemSshEphemeralKeySource source = new();
        byte[] publicKey = new byte[Sntrup761.PublicKeySize];
        byte[] secretKey = new byte[Sntrup761.SecretKeySize];
        byte[] otherPublicKey = new byte[Sntrup761.PublicKeySize];
        byte[] ciphertext = new byte[Sntrup761.CiphertextSize];
        byte[] sent = new byte[Sntrup761.SharedSecretSize];
        byte[] received = new byte[Sntrup761.SharedSecretSize];
        diagnostics.Arrange("sizes", $"public {Sntrup761.PublicKeySize}, secret {Sntrup761.SecretKeySize}, ciphertext {Sntrup761.CiphertextSize}, shared {Sntrup761.SharedSecretSize}");

        using (diagnostics.Phase("key pairs"))
        {
            source.CreateSntrup761KeyPair(publicKey, secretKey);
            source.CreateSntrup761KeyPair(otherPublicKey, new byte[Sntrup761.SecretKeySize]);
        }

        using (diagnostics.Phase("encapsulate"))
        {
            Sntrup761.Encapsulate(publicKey, ciphertext, sent);
        }

        using (diagnostics.Phase("decapsulate"))
        {
            Sntrup761.Decapsulate(secretKey, ciphertext, received);
        }

        diagnostics.Bytes("public key", publicKey);
        diagnostics.Bytes("other public key", otherPublicKey);
        diagnostics.Bytes("ciphertext", ciphertext);
        diagnostics.Act("shared secret sent", Convert.ToHexString(sent));
        diagnostics.Act("shared secret received", Convert.ToHexString(received));
        diagnostics.Diff("shared secret", sent, received);
        diagnostics.Assert("public keys differ", true, !publicKey.AsSpan().SequenceEqual(otherPublicKey));
        CollectionAssert.AreEqual(sent, received);
        CollectionAssert.AreNotEqual(publicKey, otherPublicKey);
    }
}
