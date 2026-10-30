using System.Text;
using Curl.Extension.Redis.Client;

namespace Curl.Extension.Redis.Wire;

/// <summary>Checks reading replies against RESP3's "RESP protocol description" and its sections for each type.</summary>
[TestClass]
public sealed class RespReplyReaderTests
{
    [TestMethod]
    [DataRow("+OK\r\n", nameof(RespReplyKind.SimpleString), "OK")]
    [DataRow("-ERR unknown\r\n", nameof(RespReplyKind.SimpleError), "ERR unknown")]
    [DataRow(":-42\r\n", nameof(RespReplyKind.Integer), "-42")]
    [DataRow(":+7\r\n", nameof(RespReplyKind.Integer), "+7")]
    [DataRow("$5\r\nhe\r\no\r\n", nameof(RespReplyKind.BulkString), "he\r\no")]
    [DataRow("$0\r\n\r\n", nameof(RespReplyKind.BulkString), "")]
    [DataRow("_\r\n", nameof(RespReplyKind.Null), "")]
    [DataRow("$-1\r\n", nameof(RespReplyKind.Null), "")]
    [DataRow("*-1\r\n", nameof(RespReplyKind.Null), "")]
    [DataRow("#t\r\n", nameof(RespReplyKind.Boolean), "t")]
    [DataRow("#f\r\n", nameof(RespReplyKind.Boolean), "f")]
    [DataRow(",1.23\r\n", nameof(RespReplyKind.Double), "1.23")]
    [DataRow(",-inf\r\n", nameof(RespReplyKind.Double), "-inf")]
    [DataRow(",nan\r\n", nameof(RespReplyKind.Double), "nan")]
    [DataRow(",inf\r\n", nameof(RespReplyKind.Double), "inf")]
    [DataRow("(3492890328409238509324850943850943825024385\r\n", nameof(RespReplyKind.BigNumber), "3492890328409238509324850943850943825024385")]
    [DataRow("!21\r\nSYNTAX invalid syntax\r\n", nameof(RespReplyKind.BulkError), "SYNTAX invalid syntax")]
    [DataRow("=15\r\ntxt:Some string\r\n", nameof(RespReplyKind.VerbatimString), "Some string")]
    public async Task ReadAsync_Scalar_GivesItsKindAndPayload(string wire, string kind, string payload)
    {
        RespReply reply = (await ReadAsync(wire))!;

        Assert.AreEqual(Enum.Parse<RespReplyKind>(kind), reply.Kind);
        Assert.AreEqual(payload, reply.Text);
        Assert.IsEmpty(reply.Elements);
    }

    [TestMethod]
    [DataRow("*2\r\n:1\r\n$3\r\nfoo\r\n", nameof(RespReplyKind.Array))]
    [DataRow("~2\r\n:1\r\n$3\r\nfoo\r\n", nameof(RespReplyKind.Set))]
    [DataRow(">2\r\n:1\r\n$3\r\nfoo\r\n", nameof(RespReplyKind.Push))]
    [DataRow("%1\r\n:1\r\n$3\r\nfoo\r\n", nameof(RespReplyKind.Map))]
    public async Task ReadAsync_Aggregate_GivesItsElementsInOrder(string wire, string kind)
    {
        RespReply reply = (await ReadAsync(wire))!;

        Assert.AreEqual(Enum.Parse<RespReplyKind>(kind), reply.Kind);
        CollectionAssert.AreEqual(new[] { "1", "foo" }, reply.Elements.Select(element => element.Text).ToArray());
    }

    [TestMethod]
    public async Task ReadAsync_NestedArray_KeepsTheNesting()
    {
        RespReply reply = (await ReadAsync("*2\r\n*1\r\n+a\r\n*0\r\n"))!;

        Assert.AreEqual("a", reply.Elements[0].Elements[0].Text);
        Assert.IsEmpty(reply.Elements[1].Elements);
    }

    [TestMethod]
    public async Task ReadAsync_Attribute_IsDroppedBeforeTheReplyItDescribes()
    {
        RespReply reply = (await ReadAsync("|1\r\n+key-popularity\r\n%1\r\n$1\r\na\r\n,0.19\r\n*1\r\n:2039123\r\n"))!;

        Assert.AreEqual(RespReplyKind.Array, reply.Kind);
        Assert.AreEqual("2039123", reply.Elements.Single().Text);
    }

    [TestMethod]
    public async Task ReadAsync_RepliesSplitAcrossReads_ReadsThemInTurn()
    {
        RespReplyReader reader = new(new ScriptedRedisConnection(Bytes("$1"), Bytes("0\r\n01234"), Bytes("56789\r"), Bytes("\n+O"), Bytes("K\r\n")), CancellationToken.None);

        Assert.AreEqual("0123456789", (await reader.ReadAsync())!.Text);
        Assert.AreEqual("OK", (await reader.ReadAsync())!.Text);
        Assert.IsNull(await reader.ReadAsync());
    }

    [TestMethod]
    public async Task ReadAsync_BulkStringLongerThanTheBuffer_ReadsItWhole()
    {
        string value = new('x', 20000);

        RespReply reply = (await ReadAsync($"${value.Length}\r\n{value}\r\n"))!;

        Assert.AreEqual(value, reply.Text);
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionClosedBeforeAReply_GivesNull() =>
        Assert.IsNull(await ReadAsync(string.Empty));

    [TestMethod]
    [DataRow("+OK")]
    [DataRow("$5\r\nab")]
    [DataRow("*2\r\n:1\r\n")]
    public async Task ReadAsync_ConnectionClosedInsideAReply_ThrowsIOException(string wire) =>
        await Assert.ThrowsExactlyAsync<IOException>(async () => await ReadAsync(wire));

    [TestMethod]
    [DataRow("?x\r\n", "unknown type")]
    [DataRow("\r\n", "unknown type")]
    [DataRow("+OK\n", "line not ended by CR LF")]
    [DataRow(":12a\r\n", "bad Integer value")]
    [DataRow("(1.5\r\n", "bad BigNumber value")]
    [DataRow("#x\r\n", "bad Boolean value")]
    [DataRow("_x\r\n", "bad Null value")]
    [DataRow(",abc\r\n", "bad Double value")]
    [DataRow("$-2\r\n", "bad length")]
    [DataRow("*x\r\n", "bad length")]
    [DataRow("$1\r\nab\r\n", "bulk data longer than its length")]
    [DataRow("=3\r\ntxt\r\n", "verbatim string without a format")]
    [DataRow("=4\r\ntxt-\r\n", "verbatim string without a format")]
    public async Task ReadAsync_ReplyBreakingTheSpecification_ThrowsFormatException(string wire, string detail)
    {
        FormatException failure = await Assert.ThrowsExactlyAsync<FormatException>(async () => await ReadAsync(wire));

        Assert.AreEqual($"Malformed RESP3 reply: {detail}", failure.Message);
    }

    [TestMethod]
    public async Task ReadAsync_AggregatesNestedPastTheLimit_ThrowsFormatException()
    {
        string tooDeep = string.Concat(Enumerable.Repeat("*1\r\n", RespReplyReader.MaximumNestingDepth + 1)) + ":1\r\n";
        string deepest = string.Concat(Enumerable.Repeat("*1\r\n", RespReplyReader.MaximumNestingDepth)) + ":1\r\n";

        Assert.IsNotNull(await ReadAsync(deepest));
        await Assert.ThrowsExactlyAsync<FormatException>(async () => await ReadAsync(tooDeep));
    }

    [TestMethod]
    public async Task ReadAsync_AttributeWithNullCount_IsDropped() =>
        Assert.AreEqual("OK", (await ReadAsync("|-1\r\n+OK\r\n"))!.Text);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static async Task<RespReply?> ReadAsync(string wire) =>
        await new RespReplyReader(ScriptedRedisConnection.Replaying(wire), CancellationToken.None).ReadAsync();
}
