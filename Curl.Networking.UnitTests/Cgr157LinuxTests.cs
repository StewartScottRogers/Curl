using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins CGR-157's Linux half (ADR-0956): the OpenSSL reference build (curl 8.18.0 on Ubuntu, as curl
/// 8.22.0's default OpenSSL build) lists no <c>HTTPSRR</c>, so off Windows too a direct connect's
/// <c>https://foo.test:21805/x</c> under <c>--doh-url</c> asks only for the addresses, never the
/// <c>_21805._https.foo.test</c> HTTPS (type 65) record.
/// </summary>
[TestClass]
public sealed class Cgr157LinuxTests
{
    private const int OriginPort = 21805;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void AsksHttpsRecords_OffWindows_DefaultsToOff()
    {
        var connector = new TcpConnector(new RecordingHttpsOriginResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        Diagnostics.Assert("AsksHttpsRecords", false, connector.AsksHttpsRecords);
        Assert.IsFalse(connector.AsksHttpsRecords);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_ToAnHttpsOriginOffWindows_AsksOnlyForTheAddresses()
    {
        var resolver = new RecordingHttpsOriginResolver();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());
        Diagnostics.Arrange("target", $"https://foo.test:{OriginPort}/x");

        _ = await connector.ConnectAsync(new ConnectTarget("foo.test", OriginPort, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);

        Diagnostics.Assert("look-ups", "addresses foo.test", string.Join(" | ", resolver.Calls));
        CollectionAssert.AreEqual(new[] { "addresses foo.test" }, resolver.Calls);
    }

    private sealed class RecordingHttpsOriginResolver : IHttpsOriginDnsResolver
    {
        public List<string> Calls { get; } = [];

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Calls.Add($"addresses {host}");
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveHttpsOriginAsync(string host, int port, CancellationToken cancellationToken)
        {
            Calls.Add($"https-origin {host}:{port}");
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }

        public ServiceBindingRecord? HttpsRecordFor(string host, int port) => null;
    }
}
