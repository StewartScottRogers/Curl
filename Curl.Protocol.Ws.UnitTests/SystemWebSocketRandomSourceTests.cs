namespace Curl.Protocol.Ws;

[TestClass]
public sealed class SystemWebSocketRandomSourceTests
{
    [TestMethod]
    public void Fill_TwoBuffers_FillsThemDifferently()
    {
        var source = new SystemWebSocketRandomSource();
        byte[] first = new byte[32];
        byte[] second = new byte[32];

        source.Fill(first);
        source.Fill(second);

        CollectionAssert.AreNotEqual(first, second);
    }
}
