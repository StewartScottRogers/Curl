using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Redis.RedisCaseRun;

namespace Curl.Conformance.Extended.UnitTests.Redis;

/// <summary>Every RESP3 reply type ("RESP protocol description") as the reply to <c>GET k</c> after <c>HELLO 3</c>, rendered as <c>redis-cli --raw</c> does (ADR-1079).</summary>
[TestClass]
public sealed class RedisReplyTypeCases
{
    private const string Url = "redis://localhost/GET/k";

    private static readonly string HelloThenGet = Command("HELLO", "3") + Command("GET", "k");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Request-1")]
    [SpecRequirement("Redis:RESP-SimpleString-1")]
    [SpecRequirement("Redis:HELLO-1")]
    [SpecRequirement("Redis:URI-1")]
    public Task SimpleString_ReplyOk_PrintsItsText() =>
        CaseAsync(["Redis:RESP-Request-1", "Redis:RESP-SimpleString-1", "Redis:HELLO-1", "Redis:URI-1"], 0, "OK\n", string.Empty, "+OK\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-SimpleError-1")]
    public Task SimpleError_Reply_FailsWithWeirdServerReplyAndItsText() =>
        CaseAsync(["Redis:RESP-SimpleError-1"], 8, string.Empty, "curl: (8) Redis server answered GET with error: ERR no such key\n", "-ERR no such key\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Integer-1")]
    public Task Integer_NegativeReply_PrintsItsDigits() =>
        CaseAsync(["Redis:RESP-Integer-1"], 0, "-42\n", string.Empty, ":-42\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-BulkString-1")]
    public Task BulkString_HoldingCrLf_PrintsItsBytesByLength() =>
        CaseAsync(["Redis:RESP-BulkString-1"], 0, "he\r\nllo\n", string.Empty, "$7\r\nhe\r\nllo\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Array-1")]
    public Task Array_OfTwoElements_PrintsEachElement() =>
        CaseAsync(["Redis:RESP-Array-1"], 0, "a\n1\n", string.Empty, "*2\r\n+a\r\n:1\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Null-1")]
    public Task Null_Reply_PrintsAnEmptyLine() =>
        CaseAsync(["Redis:RESP-Null-1"], 0, "\n", string.Empty, "_\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Boolean-1")]
    public Task Boolean_False_PrintsFalse() =>
        CaseAsync(["Redis:RESP-Boolean-1"], 0, "false\n", string.Empty, "#f\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Double-1")]
    public Task Double_WithExponent_PrintsItsText() =>
        CaseAsync(["Redis:RESP-Double-1"], 0, "1.5e3\n", string.Empty, ",1.5e3\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-BigNumber-1")]
    public Task BigNumber_PastSixtyFourBits_PrintsEveryDigit() =>
        CaseAsync(["Redis:RESP-BigNumber-1"], 0, "3492890328409238509324850943850943825024385\n", string.Empty, "(3492890328409238509324850943850943825024385\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-BulkError-1")]
    public Task BulkError_Reply_FailsWithWeirdServerReplyAndItsText() =>
        CaseAsync(["Redis:RESP-BulkError-1"], 8, string.Empty, "curl: (8) Redis server answered GET with error: SYNTAX invalid syntax\n", "!21\r\nSYNTAX invalid syntax\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-VerbatimString-1")]
    public Task VerbatimString_Txt_PrintsTheTextAfterItsFormat() =>
        CaseAsync(["Redis:RESP-VerbatimString-1"], 0, "Some string\n", string.Empty, "=15\r\ntxt:Some string\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Map-1")]
    public Task Map_OfTwoEntries_PrintsEachKeyAndValue() =>
        CaseAsync(["Redis:RESP-Map-1"], 0, "first\n1\nsecond\n2\n", string.Empty, "%2\r\n+first\r\n:1\r\n+second\r\n:2\r\n");

    [TestMethod]
    [SpecRequirement("Redis:RESP-Set-1")]
    public Task Set_OfTwoMembers_PrintsEachMember() =>
        CaseAsync(["Redis:RESP-Set-1"], 0, "orange\napple\n", string.Empty, "~2\r\n+orange\r\n+apple\r\n");

    private static Task CaseAsync(string[] requirements, int exitCode, string standardOutput, string standardError, params string[] reply) =>
        Known.RunCaseAsync(
            requirements,
            async () => AssertRun(await RunAsync(HelloThen(reply), Url), requirements[0], exitCode, standardOutput, standardError, HelloThenGet));
}
