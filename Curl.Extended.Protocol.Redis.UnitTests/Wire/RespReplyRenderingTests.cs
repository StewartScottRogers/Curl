using System.Text;
using Curl.Extended.Protocol.Redis.Client;

namespace Curl.Extended.Protocol.Redis.Wire;

/// <summary>Checks how each RESP3 reply type is written to the output (ADR-1079).</summary>
[TestClass]
public sealed class RespReplyRenderingTests
{
    [TestMethod]
    [DataRow("+OK\r\n", "OK\n")]
    [DataRow("$3\r\na\nb\r\n", "a\nb\n")]
    [DataRow(":5\r\n", "5\n")]
    [DataRow("_\r\n", "\n")]
    [DataRow("$-1\r\n", "\n")]
    [DataRow("#t\r\n", "true\n")]
    [DataRow("#f\r\n", "false\n")]
    [DataRow(",3.5\r\n", "3.5\n")]
    [DataRow("=7\r\ntxt:hey\r\n", "hey\n")]
    [DataRow("*3\r\n+a\r\n*1\r\n:1\r\n-ERR x\r\n", "a\n1\nERR x\n")]
    [DataRow("%2\r\n+k1\r\n:1\r\n+k2\r\n_\r\n", "k1\n1\nk2\n\n")]
    [DataRow("*0\r\n", "")]
    public async Task Render_Reply_WritesItAsRedisCliRawDoes(string wire, string rendered)
    {
        RespReply reply = (await new RespReplyReader(ScriptedRedisConnection.Replaying(wire), CancellationToken.None).ReadAsync())!;

        Assert.AreEqual(rendered, Encoding.UTF8.GetString(RespReplyRendering.Render(reply)));
    }
}
