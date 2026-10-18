namespace Curl.Conformance;

[TestClass]
public sealed class ZeroMaskWebSocketRandomSourceTests
{
    [TestMethod]
    public void Fill_FrameMaskLength_GivesAZeroMask()
    {
        ZeroMaskWebSocketRandomSource source = new();
        byte[] mask = [0xFF, 0xFF, 0xFF, 0xFF];

        source.Fill(mask);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x00 }, mask);
    }

    [TestMethod]
    public void Fill_KeyLength_GivesRandomBytesThatDifferFromOneFillToTheNext()
    {
        ZeroMaskWebSocketRandomSource source = new();
        byte[] firstKey = new byte[16];
        byte[] secondKey = new byte[16];

        source.Fill(firstKey);
        source.Fill(secondKey);

        CollectionAssert.AreNotEqual(firstKey, secondKey);
    }
}
