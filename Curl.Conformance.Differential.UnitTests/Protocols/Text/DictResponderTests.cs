using System.Text;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Text;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Text;

[TestClass]
public sealed class DictResponderTests
{
    private const string Banner = "220 dict ready <auth.mime> <1.1@x>\r\n";

    [TestMethod]
    [DataRow("DEFINE ! word", "150 1 definitions retrieved\r\n151 \"word\" web1913 \"Webster\"\r\nthe definition\r\n.\r\n250 ok\r\n")]
    [DataRow("define ! word", "150 1 definitions retrieved\r\n151 \"word\" web1913 \"Webster\"\r\nthe definition\r\n.\r\n250 ok\r\n")]
    [DataRow("MATCH ! prefix w", "152 1 matches found\r\nweb1913 \"word\"\r\n.\r\n250 ok\r\n")]
    [DataRow("match x", "152 1 matches found\r\nweb1913 \"word\"\r\n.\r\n250 ok\r\n")]
    [DataRow("SHOW DB", "500 unknown\r\n")]
    [DataRow("", "500 unknown\r\n")]
    [DataRow("QUIT", "221 bye\r\n")]
    [DataRow("quit now", "221 bye\r\n")]
    public async Task RespondAsync_Command_AnswersAndLogsTheLine(string line, string reply)
    {
        var (output, log) = await RunAsync(line + "\r\n");

        Assert.AreEqual(Banner + reply, output);
        Assert.AreEqual(line, log);
    }

    [TestMethod]
    public async Task RespondAsync_QuitEndsTheConnection_LaterLinesAreNotRead()
    {
        var (output, log) = await RunAsync("QUIT\r\nDEFINE x\r\n");

        Assert.AreEqual(Banner + "221 bye\r\n", output);
        Assert.AreEqual("QUIT", log);
    }

    [TestMethod]
    public async Task RespondAsync_ClientHangsUp_EndsAfterTheLastReply()
    {
        var (output, log) = await RunAsync("MATCH a\r\nFOO\r\n");

        Assert.AreEqual(Banner + "152 1 matches found\r\nweb1913 \"word\"\r\n.\r\n250 ok\r\n500 unknown\r\n", output);
        Assert.AreEqual("MATCH a\nFOO", log);
    }

    private static async Task<(string Output, string Log)> RunAsync(string input)
    {
        var log = new ProtocolWireLog();
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(input));

        await new DictResponder(log).RespondAsync(connection, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), Encoding.UTF8.GetString(log.Take()));
    }
}
