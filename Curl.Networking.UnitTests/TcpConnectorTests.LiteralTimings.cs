using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="TcpConnector" /> records a name-lookup time for a literal address, as
/// curl 8.21.0 prints a non-zero <c>%{time_namelookup}</c> for <c>http://127.0.0.1/</c>
/// (measured, ADR-0030, BL-287 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ToALiteralAddress_ResolvesItAndRecordsTheNameLookupTime()
    {
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = endPoint => new FakeConnection { RemoteEndPoint = endPoint } };
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new SteppingTimeProvider(100));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 80, UseTls: false));

        Diagnostics.Assert("resolved hosts", "127.0.0.1", string.Join(" | ", resolver.ResolvedHosts));
        CollectionAssert.AreEqual(new[] { "127.0.0.1" }, resolver.ResolvedHosts);
        Assert.AreEqual(110L, result.Timings!.NameResolved);
        Assert.IsTrue(result.Timings.NameResolved > result.Timings.Started, "time_namelookup is not zero.");
    }
}
