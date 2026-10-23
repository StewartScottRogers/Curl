using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Core;

[TestClass]
public sealed class ProtocolCoreTests
{
    [TestMethod]
    public async Task ReadLineAsync_Lines_StripsTerminatorsAndEndsWithNull()
    {
        using var stream = new MemoryStream("one\r\ntwo\nthree"u8.ToArray());
        var reader = new ProtocolLineReader(stream);

        Assert.AreEqual("one", await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("two", await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("three", await reader.ReadLineAsync(CancellationToken.None));
        Assert.IsNull(await reader.ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_TextAndBytes_WritesThem()
    {
        using var stream = new DuplexMemoryStream([]);
        var writer = new ProtocolLineWriter(stream);

        await writer.SendAsync("a\r\n", CancellationToken.None);
        await writer.SendAsync("b"u8.ToArray(), CancellationToken.None);

        Assert.AreEqual("a\r\nb", Encoding.ASCII.GetString(stream.Written));
    }

    [TestMethod]
    public void Take_Lines_JoinsWithNewlineAndClears()
    {
        var log = new ProtocolWireLog();
        log.Add("USER x");
        log.Add("PASS y");

        Assert.AreEqual("USER x\nPASS y", Encoding.UTF8.GetString(log.Take()));
        Assert.IsEmpty(log.Take());
    }

    [TestMethod]
    public async Task RespondAsync_Log_AnswersAndClearsIt()
    {
        var log = new ProtocolWireLog();
        log.Add("a");
        log.Add("b");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\na\nb", await RespondAsync(log, "GET /log HTTP/1.1\r\n\r\n"));
        Assert.IsEmpty(log.Take());
    }

    [TestMethod]
    public async Task RespondAsync_Reset_ClearsLogAndAnswersOk()
    {
        var log = new ProtocolWireLog();
        log.Add("a");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", await RespondAsync(log, "GET /reset HTTP/1.1\r\n\r\n"));
        Assert.IsEmpty(log.Take());
    }

    [TestMethod]
    public async Task RespondAsync_RequestEndingBeforeTheBlankLine_StillAnswers()
    {
        var log = new ProtocolWireLog();
        log.Add("a");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\na", await RespondAsync(log, "GET /log HTTP/1.1\r\nHost: x\r\n"));
    }

    [TestMethod]
    public async Task RespondAsync_RequestLineWithoutTarget_AnswersNothing()
    {
        Assert.AreEqual(string.Empty, await RespondAsync(new ProtocolWireLog(), "GET\r\n\r\n"));
    }

    private static async Task<string> RespondAsync(ProtocolWireLog log, string request)
    {
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(request));
        await new ProtocolControlResponder(log).RespondAsync(connection, CancellationToken.None);
        return Encoding.Latin1.GetString(connection.Written);
    }
}
