using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="TcpConnector" /> reports every connection it opens itself as fresh,
/// never reused, since the protocols resend requests and resume sessions only on a reused one
/// (AF-0530, BL-4636).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithoutUseTls_ReportsTheFreshConnectionAsNotReused()
    {
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 80, UseTls: false));

        Diagnostics.Assert("is reused", false, result.IsReused);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.IsReused);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_ReportsTheFreshConnectionAsNotReused()
    {
        var connector = CreateConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true));

        Diagnostics.Assert("is reused", false, result.IsReused);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(result.IsReused);
    }
}
