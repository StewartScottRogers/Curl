namespace Curl.Extended.Protocol.SignalR.Transport;

[TestClass]
public sealed class BufferedConnectionReaderTests
{
    [TestMethod]
    public async Task ReadLineAsync_LineFeedIsTheFirstBufferedByte_ReturnsAnEmptyLineWithoutReadingMore()
    {
        BufferedConnectionReader reader = new(ScriptedConnection.FromText("\nnext\n"));

        string first = await reader.ReadLineAsync(CancellationToken.None);
        string second = await reader.ReadLineAsync(CancellationToken.None);

        Assert.AreEqual(string.Empty, first);
        Assert.AreEqual("next", second);
    }

    [TestMethod]
    public async Task ReadLineAsync_OnlyALineFeedThenTheConnectionEnds_ReturnsAnEmptyLine()
    {
        BufferedConnectionReader reader = new(ScriptedConnection.FromText("\n"));

        string line = await reader.ReadLineAsync(CancellationToken.None);

        Assert.AreEqual(string.Empty, line);
    }
}
