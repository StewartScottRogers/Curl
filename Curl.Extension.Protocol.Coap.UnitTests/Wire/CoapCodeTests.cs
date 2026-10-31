namespace Curl.Extension.Protocol.Coap.Wire;

[TestClass]
public sealed class CoapCodeTests
{
    [TestMethod]
    [DataRow(0, 1, 0x01, "0.01")]
    [DataRow(2, 5, 0x45, "2.05")]
    [DataRow(4, 4, 0x84, "4.04")]
    [DataRow(5, 0, 0xA0, "5.00")]
    [DataRow(7, 31, 0xFF, "7.31")]
    public void Of_Rfc7252Section3_PacksClassAndDetail(int codeClass, int detail, int value, string written)
    {
        CoapCode code = CoapCode.Of(codeClass, detail);

        Assert.AreEqual((byte)value, code.Value);
        Assert.AreEqual(codeClass, code.Class);
        Assert.AreEqual(detail, code.Detail);
        Assert.AreEqual(written, code.ToString());
    }

    [TestMethod]
    public void Codes_Rfc7252Section12_1_HaveTheRegisteredValues()
    {
        Assert.AreEqual("0.00", CoapCode.Empty.ToString());
        Assert.AreEqual("0.02", CoapCode.Post.ToString());
        Assert.AreEqual("0.03", CoapCode.Put.ToString());
        Assert.AreEqual("0.04", CoapCode.Delete.ToString());
        Assert.AreEqual("2.01", CoapCode.Created.ToString());
        Assert.AreEqual("2.02", CoapCode.Deleted.ToString());
        Assert.AreEqual("2.03", CoapCode.Valid.ToString());
        Assert.AreEqual("2.04", CoapCode.Changed.ToString());
        Assert.AreEqual("2.31", CoapCode.Continue.ToString());
        Assert.AreEqual("4.00", CoapCode.BadRequest.ToString());
        Assert.AreEqual("4.04", CoapCode.NotFound.ToString());
        Assert.AreEqual("4.13", CoapCode.RequestEntityTooLarge.ToString());
        Assert.AreEqual("5.00", CoapCode.InternalServerError.ToString());
    }

    [TestMethod]
    [DataRow(8, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 32)]
    [DataRow(0, -1)]
    public void Of_Rfc7252Section3_RejectsAClassOrDetailOutOfRange(int codeClass, int detail)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CoapCode.Of(codeClass, detail));
    }
}
