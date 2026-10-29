namespace Curl.Extension.Coap.Wire;

[TestClass]
public sealed class CoapUnsignedIntegerTests
{
    [TestMethod]
    [DataRow(0u, new byte[0])]
    [DataRow(1u, new byte[] { 0x01 })]
    [DataRow(255u, new byte[] { 0xFF })]
    [DataRow(256u, new byte[] { 0x01, 0x00 })]
    [DataRow(0x010000u, new byte[] { 0x01, 0x00, 0x00 })]
    [DataRow(uint.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    public void EncodeAndDecode_Rfc7252Section3_2_RoundTripsInTheFewestBytes(uint value, byte[] bytes)
    {
        CollectionAssert.AreEqual(bytes, CoapUnsignedInteger.Encode(value));
        Assert.AreEqual(value, CoapUnsignedInteger.Decode(bytes, 4));
    }


    [TestMethod]
    public void Decode_Rfc7252Section3_2_RejectsAValueLongerThanTheOptionAllows()
    {
        Assert.ThrowsExactly<FormatException>(() => CoapUnsignedInteger.Decode(new byte[3], 2));
    }

}
