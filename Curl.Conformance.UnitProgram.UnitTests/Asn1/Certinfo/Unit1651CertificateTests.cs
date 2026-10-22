using Curl.Conformance.UnitProgram.Asn1.Certinfo;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Certinfo;

[TestClass]
public sealed class Unit1651CertificateTests
{
    [TestMethod]
    public void Create_TheCapturedCertificate_Is3935BytesOfOneDerSequence()
    {
        byte[] certificate = Unit1651Certificate.Create();

        Assert.HasCount(3935, certificate);
        Assert.AreEqual((byte)0x30, certificate[0]);
        Assert.AreEqual((byte)0xFB, certificate[^1]);
    }

    [TestMethod]
    public void Create_TwoCalls_ReturnSeparateCopies()
    {
        byte[] first = Unit1651Certificate.Create();
        first[0] = 0;

        Assert.AreEqual((byte)0x30, Unit1651Certificate.Create()[0]);
    }
}
