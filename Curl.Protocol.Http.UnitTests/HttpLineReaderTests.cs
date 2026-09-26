using System.Text;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpLineReader" /> on its own: lines come out whole however the bytes are
/// split, and whatever follows the last line is kept.
/// </summary>
[TestClass]
public sealed class HttpLineReaderTests
{
    [TestMethod]
    [DataRow(1, DisplayName = "1-byte reads")]
    [DataRow(3, DisplayName = "3-byte reads, CRLF split across them")]
    [DataRow(65536, DisplayName = "One read")]
    public async Task ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest(int chunkSize)
    {
        HttpLineReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("ab\r\nc\nrest"), chunkSize));

        byte[]? first = await reader.ReadLineAsync(false, CancellationToken.None);
        byte[]? second = await reader.ReadLineAsync(false, CancellationToken.None);
        byte[] rest = reader.TakeRemaining();

        Assert.AreEqual("ab\r\n", Encoding.ASCII.GetString(first!));
        Assert.AreEqual("c\n", Encoding.ASCII.GetString(second!));
        Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReadLineAsync_PeerClosesInsideALine_ReturnsNull()
    {
        HttpLineReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("partial"), 2));

        byte[]? line = await reader.ReadLineAsync(false, CancellationToken.None);

        Assert.IsNull(line);
    }

    [TestMethod]
    public async Task ReadLineAsync_LinesLongerThanTheFirstBuffer_ComeOutWhole()
    {
        string longLine = new string('a', 40000) + "\r\n";
        HttpLineReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("x\n" + longLine + longLine), 5000));

        await reader.ReadLineAsync(false, CancellationToken.None);
        byte[]? first = await reader.ReadLineAsync(false, CancellationToken.None);
        byte[]? second = await reader.ReadLineAsync(false, CancellationToken.None);

        Assert.AreEqual(longLine, Encoding.ASCII.GetString(first!));
        Assert.AreEqual(longLine, Encoding.ASCII.GetString(second!));
    }
}
