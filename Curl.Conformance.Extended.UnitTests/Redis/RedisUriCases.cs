using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Redis.RedisCaseRun;

namespace Curl.Conformance.Extended.UnitTests.Redis;

/// <summary>The redis and rediss URI schemes (IANA provisional registrations): the port and TLS.</summary>
[TestClass]
public sealed class RedisUriCases
{
    [TestMethod]
    [SpecRequirement("Redis:URI-5")]
    public Task Port_InTheAuthority_IsTheOneConnectedTo() =>
        Known.RunCaseAsync(
            ["Redis:URI-5"],
            async () => AssertRun(
                await RunOnPortAsync(HelloThen("+PONG\r\n"), 16379, "redis://localhost:16379/PING"),
                "Redis:URI-5",
                0,
                "PONG\n",
                string.Empty,
                Command("HELLO", "3") + Command("PING")));

    [TestMethod]
    [SpecRequirement("Redis:URI-6")]
    public Task Rediss_RunsTheSameExchangeOverTls() =>
        Known.RunCaseAsync(
            ["Redis:URI-6"],
            async () =>
            {
                CurlRunResult result = await RunTlsAsync(HelloThen("$2\r\nhi\r\n"), "rediss://localhost/GET/k");

                AssertRun(result, "Redis:URI-6", 0, "hi\n", string.Empty, Command("HELLO", "3") + Command("GET", "k"));
                Assert.AreEqual(ReferenceTransport.Tls, result.Exchanges.Single().Transport, "Redis:URI-6");
            });
}
