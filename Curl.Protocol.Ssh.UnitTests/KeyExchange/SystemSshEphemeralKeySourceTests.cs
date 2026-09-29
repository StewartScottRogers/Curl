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
}
