namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class TlsReaderTests
{
    [TestMethod]
    public void ReadVector16_LengthPastTheEnd_Throws()
    {
        var reader = new TlsReader(new byte[] { 0, 3, 1 });

        Assert.ThrowsExactly<FormatException>(() => reader.ReadVector16());
    }

    [TestMethod]
    public void ExpectEnd_EveryByteRead_DoesNotThrow()
    {
        var reader = new TlsReader(new byte[] { 0, 1, 7 });

        var vector = reader.ReadVector16();
        reader.ExpectEnd();

        CollectionAssert.AreEqual(new byte[] { 7 }, vector.ToArray());
        Assert.IsTrue(reader.IsAtEnd);
    }
}
