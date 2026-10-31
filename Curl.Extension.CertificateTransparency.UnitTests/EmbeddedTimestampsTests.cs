namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class EmbeddedTimestampsTests
{
    [TestMethod]
    public void Of_Rfc9162Section8_1_2_CertificateWithTheList_GivesTheListAndThePrecertificateTbs()
    {
        using var chain = new CertificateTransparencyChain();
        byte[] list = [0, 0];

        var embedded = EmbeddedTimestamps.Of(chain.LeafWith(list))!;

        CollectionAssert.AreEqual(list, embedded.List.ToArray());
        CollectionAssert.AreEqual(chain.PrecertificateTbs(), embedded.TbsCertificate.ToArray());
    }

    [TestMethod]
    public void Of_Rfc6962Section3_3_CertificateWithoutTheList_GivesNull()
    {
        using var chain = new CertificateTransparencyChain();

        Assert.IsNull(EmbeddedTimestamps.Of(chain.LeafWithout()));
    }

    [TestMethod]
    public void Of_CertificateIsNotDer_Throws()
    {
        Assert.ThrowsExactly<System.Formats.Asn1.AsnContentException>(() => EmbeddedTimestamps.Of(new byte[] { 0x30, 0x05 }));
    }
}
