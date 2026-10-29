namespace Curl.Extension.Coap.Wire;

[TestClass]
public sealed class CoapObserveValueTests
{
    [TestMethod]
    [DataRow(CoapObserveValue.Register, new byte[0])]
    [DataRow(CoapObserveValue.Deregister, new byte[] { 0x01 })]
    [DataRow(0x1234u, new byte[] { 0x12, 0x34 })]
    [DataRow(CoapObserveValue.MaximumSequenceNumber, new byte[] { 0xFF, 0xFF, 0xFF })]
    public void EncodeAndDecode_Rfc7641Section2_RoundTripsInZeroToThreeBytes(uint value, byte[] bytes)
    {
        CollectionAssert.AreEqual(bytes, CoapObserveValue.Encode(value));
        Assert.AreEqual(value, CoapObserveValue.Decode(bytes));
    }


    [TestMethod]
    public void EncodeAndDecode_Rfc7641Section2_RejectsASequenceNumberPast24Bits()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CoapObserveValue.Encode(CoapObserveValue.MaximumSequenceNumber + 1));
        Assert.ThrowsExactly<FormatException>(() => CoapObserveValue.Decode(new byte[4]));
    }

}
