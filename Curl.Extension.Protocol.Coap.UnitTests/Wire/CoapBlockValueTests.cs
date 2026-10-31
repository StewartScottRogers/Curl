namespace Curl.Extension.Protocol.Coap.Wire;

[TestClass]
public sealed class CoapBlockValueTests
{
    [TestMethod]
    [DataRow(0u, false, 0, new byte[0], DisplayName = "NUM 0, 16 bytes, last")]
    [DataRow(0u, true, 2, new byte[] { 0x0A }, DisplayName = "NUM 0, 64 bytes, more")]
    [DataRow(1u, false, 6, new byte[] { 0x16 }, DisplayName = "NUM 1, 1024 bytes")]
    [DataRow(16u, true, 6, new byte[] { 0x01, 0x0E }, DisplayName = "NUM 16, two bytes")]
    [DataRow(CoapBlockValue.MaximumNumber, true, 6, new byte[] { 0xFF, 0xFF, 0xFE }, DisplayName = "largest NUM")]
    public void Encode_Rfc7959Section2_2_PacksNumMoreAndSzx(uint number, bool more, int sizeExponent, byte[] bytes)
    {
        CoapBlockValue block = new(number, more, sizeExponent);

        CollectionAssert.AreEqual(bytes, block.Encode());
        Assert.AreEqual(block, CoapBlockValue.Decode(bytes));
    }


    [TestMethod]
    [DataRow(0, 16)]
    [DataRow(6, 1024)]
    public void Size_Rfc7959Section2_2_SizeIsTwoToTheSzxPlusFour(int sizeExponent, int size)
    {
        Assert.AreEqual(size, new CoapBlockValue(0, false, sizeExponent).Size);
    }


    [TestMethod]
    [DataRow(CoapBlockValue.MaximumNumber + 1, 0)]
    [DataRow(0u, 7)]
    [DataRow(0u, -1)]
    public void Encode_Rfc7959Section2_2_RejectsNumOrSzxOutOfRange(uint number, int sizeExponent)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CoapBlockValue(number, false, sizeExponent).Encode());
    }


    [TestMethod]
    [DataRow(new byte[] { 0x07 }, DisplayName = "reserved SZX 7")]
    [DataRow(new byte[] { 0x00, 0x00, 0x00, 0x10 }, DisplayName = "four bytes")]
    public void Decode_Rfc7959Section2_2_RejectsAMalformedValue(byte[] bytes)
    {
        Assert.ThrowsExactly<FormatException>(() => CoapBlockValue.Decode(bytes));
    }

}
