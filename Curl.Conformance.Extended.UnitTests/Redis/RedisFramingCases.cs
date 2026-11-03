using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Redis.RedisCaseRun;

namespace Curl.Conformance.Extended.UnitTests.Redis;

/// <summary>Replies whose framing a client must follow - nested aggregates, an attribute or a push frame before the reply, a reply split across reads - and binary arguments.</summary>
[TestClass]
public sealed class RedisFramingCases
{
    private const string Url = "redis://localhost/GET/k";

    private static readonly string HelloThenGet = Command("HELLO", "3") + Command("GET", "k");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Aggregate-1")]
    public Task NestedAggregates_ArrayHoldingArrayAndMapOfSet_PrintsEveryLeaf() =>
        CaseAsync(["Redis:RESP-Aggregate-1"], "1\n2\nk\nv\n", "*2\r\n*2\r\n:1\r\n:2\r\n%1\r\n+k\r\n~1\r\n+v\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Attribute-1")]
    public Task Attribute_BeforeTheReply_IsSkippedAndTheReplyPrinted() =>
        CaseAsync(
            ["Redis:RESP-Attribute-1"],
            "2039123\n9543892\n",
            "|1\r\n+key-popularity\r\n%2\r\n$1\r\na\r\n,0.1923\r\n$1\r\nb\r\n,0.0012\r\n*2\r\n:2039123\r\n:9543892\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Push-1")]
    public Task Push_BeforeTheReply_IsNotTakenForTheReply() =>
        CaseAsync(["Redis:RESP-Push-1"], "OK\n", ">2\r\n+message\r\n+hello\r\n", "+OK\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Framing-1")]
    public Task SplitReply_AcrossThreeReads_PrintsTheWholeBulkString() =>
        CaseAsync(["Redis:RESP-Framing-1"], "hello world\n", "$1", "1\r\nhello", " world\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Request-2")]
    public Task BinaryArguments_NulAndCrLfInPathAndData_AreSentAsBulkStringsByLength() =>
        Known.RunCaseAsync(
            ["Redis:RESP-Request-2"],
            async () => AssertRun(
                await RunAsync(HelloThen("+OK\r\n"), "-d", "v\r\nz", "redis://localhost/SET/k%00%0D%0Ax"),
                "Redis:RESP-Request-2",
                0,
                "OK\n",
                string.Empty,
                Command("HELLO", "3") + Command("SET", "k\0\r\nx", "v\r\nz")));

    private static Task CaseAsync(string[] requirements, string standardOutput, params string[] reply) =>
        Known.RunCaseAsync(
            requirements,
            async () => AssertRun(await RunAsync(HelloThen(reply), Url), requirements[0], 0, standardOutput, string.Empty, HelloThenGet));
}
