using System.Text;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpLineReader" /> on its own: lines come out whole however the bytes are
/// split, and whatever follows the last line is kept.
/// </summary>
[TestClass]
public sealed class HttpLineReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(1, DisplayName = "1-byte reads")]
    [DataRow(3, DisplayName = "3-byte reads, CRLF split across them")]
    [DataRow(65536, DisplayName = "One read")]
    public async Task ReadLineAsync_AnySplit_ReturnsWholeLinesThenTheRest(int chunkSize)
    {
        HttpLineReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("ab\r\nc\nrest"), chunkSize));
        Diagnostics.Arrange("chunk size", chunkSize);
        Diagnostics.Bytes("scripted", Encoding.ASCII.GetBytes("ab\r\nc\nrest"));

        byte[]? first = await reader.ReadLineAsync(false, CancellationToken.None);
        byte[]? second = await reader.ReadLineAsync(false, CancellationToken.None);
        byte[] rest = reader.TakeRemaining();

        Diagnostics.Bytes("first line", first);
        Diagnostics.Bytes("second line", second);
        Diagnostics.Bytes("rest", rest);
        Diagnostics.Act("rest", Encoding.ASCII.GetString(rest));
        Diagnostics.Diff("first line", "ab\r\n", Encoding.ASCII.GetString(first!));
        Diagnostics.Diff("second line", "c\n", Encoding.ASCII.GetString(second!));
        Assert.AreEqual("ab\r\n", Encoding.ASCII.GetString(first!));
        Assert.AreEqual("c\n", Encoding.ASCII.GetString(second!));
        Assert.IsTrue("rest".StartsWith(Encoding.ASCII.GetString(rest), StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReadLineAsync_PeerClosesInsideALine_ReturnsNull()
    {
        HttpLineReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("partial"), 2));
        Diagnostics.Arrange("scripted, chunk size", "partial, 2");

        byte[]? line = await reader.ReadLineAsync(false, CancellationToken.None);

        Diagnostics.Act("line", line is null ? "(none)" : Encoding.ASCII.GetString(line));
        Diagnostics.Assert("line", "(none)", line is null ? "(none)" : Encoding.ASCII.GetString(line));
        Assert.IsNull(line);
    }

    [TestMethod]
    public async Task ReadLineAsync_LinesLongerThanTheFirstBuffer_ComeOutWhole()
    {
        string longLine = new string('a', 40000) + "\r\n";
        HttpLineReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("x\n" + longLine + longLine), 5000));
        Diagnostics.Arrange("lines, chunk size", "x, then two lines of 40000 'a' and CRLF, 5000");

        await reader.ReadLineAsync(false, CancellationToken.None);
        byte[]? first = await reader.ReadLineAsync(false, CancellationToken.None);
        byte[]? second = await reader.ReadLineAsync(false, CancellationToken.None);

        Diagnostics.Act("line lengths", $"{first?.Length}, {second?.Length}");
        Diagnostics.Diff("first long line", longLine, Encoding.ASCII.GetString(first!));
        Diagnostics.Diff("second long line", longLine, Encoding.ASCII.GetString(second!));
        Assert.AreEqual(longLine, Encoding.ASCII.GetString(first!));
        Assert.AreEqual(longLine, Encoding.ASCII.GetString(second!));
    }
}
