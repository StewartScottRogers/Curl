using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Redis.RedisCaseRun;

namespace Curl.Conformance.Extended.UnitTests.Redis;

/// <summary>The connection handshake: <c>HELLO 3</c> and its fallback to RESP2, authentication, and <c>SELECT</c> from the path.</summary>
[TestClass]
public sealed class RedisHandshakeCases
{
    private const string RefusedHello = "-ERR unknown command 'HELLO', with args beginning with: '3' \r\n";

    [TestMethod]
    [SpecRequirement("Redis:HELLO-2")]
    [SpecRequirement("Redis:RESP2-Null-1")]
    public Task Resp2_ServerRefusesHelloAndAnswersNullBulkString_GoesOnInResp2() =>
        Known.RunCaseAsync(
            ["Redis:HELLO-2", "Redis:RESP2-Null-1"],
            async () => AssertRun(
                await RunAsync(name => name == "HELLO" ? [RefusedHello] : ["$-1\r\n"], "redis://localhost/GET/k"),
                "Redis:HELLO-2",
                0,
                "\n",
                string.Empty,
                Command("HELLO", "3") + Command("GET", "k")));

    [TestMethod]
    [SpecRequirement("Redis:RESP2-Null-2")]
    public Task Resp2_ServerRefusesHelloAndAnswersNullArray_PrintsNothing() =>
        Known.RunCaseAsync(
            ["Redis:RESP2-Null-2"],
            async () => AssertRun(
                await RunAsync(name => name == "HELLO" ? [RefusedHello] : ["*-1\r\n"], "redis://localhost/LRANGE/k/0/-1"),
                "Redis:RESP2-Null-2",
                0,
                string.Empty,
                string.Empty,
                Command("HELLO", "3") + Command("LRANGE", "k", "0", "-1")));

    [TestMethod]
    [SpecRequirement("Redis:HELLO-3")]
    [SpecRequirement("Redis:AUTH-1")]
    public Task Auth_FromUserOption_IsSentInHello() =>
        Known.RunCaseAsync(
            ["Redis:HELLO-3", "Redis:AUTH-1"],
            async () => AssertRun(
                await RunAsync(HelloThen("+PONG\r\n"), "-u", "alice:secret", "redis://localhost/PING"),
                "Redis:HELLO-3",
                0,
                "PONG\n",
                string.Empty,
                Command("HELLO", "3", "AUTH", "alice", "secret") + Command("PING")));

    [TestMethod]
    [SpecRequirement("Redis:URI-2")]
    public Task Auth_FromUrlUserAndPassword_IsSentInHello() =>
        Known.RunCaseAsync(
            ["Redis:URI-2"],
            async () => AssertRun(
                await RunAsync(HelloThen("+PONG\r\n"), "redis://alice:secret@localhost/PING"),
                "Redis:URI-2",
                0,
                "PONG\n",
                string.Empty,
                Command("HELLO", "3", "AUTH", "alice", "secret") + Command("PING")));

    [TestMethod]
    [SpecRequirement("Redis:AUTH-2")]
    [SpecRequirement("Redis:URI-3")]
    public Task Auth_FromUrlPasswordWithoutUser_AuthenticatesAsDefault() =>
        Known.RunCaseAsync(
            ["Redis:AUTH-2", "Redis:URI-3"],
            async () => AssertRun(
                await RunAsync(HelloThen("+PONG\r\n"), "redis://:secret@localhost/PING"),
                "Redis:AUTH-2",
                0,
                "PONG\n",
                string.Empty,
                Command("HELLO", "3", "AUTH", "default", "secret") + Command("PING")));

    [TestMethod]
    [SpecRequirement("Redis:AUTH-3")]
    public Task Auth_ServerAnswersWrongPass_FailsWithLoginDenied() =>
        Known.RunCaseAsync(
            ["Redis:AUTH-3"],
            async () => AssertRun(
                await RunAsync(_ => ["-WRONGPASS invalid username-password pair or user is disabled.\r\n"], "-u", "alice:bad", "redis://localhost/PING"),
                "Redis:AUTH-3",
                67,
                string.Empty,
                "curl: (67) Redis server answered HELLO with error: WRONGPASS invalid username-password pair or user is disabled.\n",
                Command("HELLO", "3", "AUTH", "alice", "bad")));

    [TestMethod]
    [SpecRequirement("Redis:HELLO-4")]
    public Task Hello_ServerAnswersNoAuth_FailsWithLoginDenied() =>
        Known.RunCaseAsync(
            ["Redis:HELLO-4"],
            async () => AssertRun(
                await RunAsync(_ => ["-NOAUTH HELLO must be called with the client already authenticated\r\n"], "redis://localhost/PING"),
                "Redis:HELLO-4",
                67,
                string.Empty,
                "curl: (67) Redis server answered HELLO with error: NOAUTH HELLO must be called with the client already authenticated\n",
                Command("HELLO", "3")));

    [TestMethod]
    [SpecRequirement("Redis:SELECT-1")]
    [SpecRequirement("Redis:URI-4")]
    public Task Select_DatabaseInPath_IsSelectedBeforeTheCommand() =>
        Known.RunCaseAsync(
            ["Redis:SELECT-1", "Redis:URI-4"],
            async () => AssertRun(
                await RunAsync(name => name switch { "HELLO" => [Hello3Reply], "SELECT" => ["+OK\r\n"], _ => ["$1\r\nv\r\n"] }, "redis://localhost/2/GET/k"),
                "Redis:SELECT-1",
                0,
                "v\n",
                string.Empty,
                Command("HELLO", "3") + Command("SELECT", "2") + Command("GET", "k")));

    [TestMethod]
    [SpecRequirement("Redis:SELECT-2")]
    public Task Select_ServerAnswersOutOfRange_FailsBeforeTheCommand() =>
        Known.RunCaseAsync(
            ["Redis:SELECT-2"],
            async () => AssertRun(
                await RunAsync(HelloThen("-ERR DB index is out of range\r\n"), "redis://localhost/99/GET/k"),
                "Redis:SELECT-2",
                8,
                string.Empty,
                "curl: (8) Redis server answered SELECT with error: ERR DB index is out of range\n",
                Command("HELLO", "3") + Command("SELECT", "99")));
}
