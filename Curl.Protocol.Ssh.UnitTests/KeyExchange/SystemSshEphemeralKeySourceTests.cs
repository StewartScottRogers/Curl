using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

[TestClass]
public sealed class SystemSshEphemeralKeySourceTests
{
    [TestMethod]
    public void CreateEllipticCurveKey_GivesAFreshKeyOnTheCurve()
    {
        SystemSshEphemeralKeySource source = new();

        using ECDiffieHellman first = source.CreateEllipticCurveKey(ECCurve.NamedCurves.nistP384);
        using ECDiffieHellman second = source.CreateEllipticCurveKey(ECCurve.NamedCurves.nistP384);

        Assert.AreEqual(384, first.KeySize);
        CollectionAssert.AreNotEqual(first.ExportParameters(false).Q.X, second.ExportParameters(false).Q.X);
    }

    [TestMethod]
    public void CreateFiniteFieldKey_GivesAFreshKeyInTheGroup()
    {
        SystemSshEphemeralKeySource source = new();
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group14;
        byte[] first = new byte[group.PrimeLength];
        byte[] second = new byte[group.PrimeLength];

        using (FiniteFieldDiffieHellman key = source.CreateFiniteFieldKey(group))
        {
            Assert.AreSame(group, key.Group);
            key.ComputePublicValue(first);
        }

        using (FiniteFieldDiffieHellman key = source.CreateFiniteFieldKey(group))
        {
            key.ComputePublicValue(second);
        }

        CollectionAssert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void CreateX25519PrivateKey_FillsAFreshKey()
    {
        SystemSshEphemeralKeySource source = new();
        byte[] first = new byte[X25519.KeySize];
        byte[] second = new byte[X25519.KeySize];

        source.CreateX25519PrivateKey(first);
        source.CreateX25519PrivateKey(second);

        CollectionAssert.AreNotEqual(new byte[X25519.KeySize], first);
        CollectionAssert.AreNotEqual(first, second);
    }
}
