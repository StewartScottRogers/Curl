using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

[TestClass]
public sealed class SymmetricSuiteTests
{
    [TestMethod]
    [DataRow((ushort)0x0001, (ushort)0x0001, true)]
    [DataRow((ushort)0x0002, (ushort)0x0001, false)]
    [DataRow((ushort)0x0001, (ushort)0xffff, false)]
    public void IsSupported_KdfAndAead_IsTrueOnlyWhenBothAreImplemented(ushort kdf, ushort aead, bool expected)
    {
        Assert.AreEqual(expected, new SymmetricSuite((HpkeKdf)kdf, (HpkeAead)aead).IsSupported);
    }
}
