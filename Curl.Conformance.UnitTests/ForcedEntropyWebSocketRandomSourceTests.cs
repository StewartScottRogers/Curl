namespace Curl.Conformance;

[TestClass]
public sealed class ForcedEntropyWebSocketRandomSourceTests
{
    [TestMethod]
    public void Fill_UpstreamCasesEntropy_GivesTheirKeyThenTheirMask()
    {
        ForcedEntropyWebSocketRandomSource source = new("12345678");
        byte[] key = new byte[16];
        byte[] mask = new byte[4];

        source.Fill(key);
        source.Fill(mask);

        Assert.AreEqual("NDMyMTUzMjE2MzIxNzMyMQ==", Convert.ToBase64String(key));
        Assert.AreEqual("8321", System.Text.Encoding.ASCII.GetString(mask));
    }

    [TestMethod]
    public void Fill_EntropyShorterThanFourBytesAndAnOddLength_PadsTheSeedWithZerosAndCutsTheLastValue()
    {
        ForcedEntropyWebSocketRandomSource source = new("12");
        byte[] destination = new byte[6];

        source.Fill(destination);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x32, 0x31, 0x01, 0x00 }, destination);
    }
}
