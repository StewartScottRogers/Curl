using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins CGR-157 (ADR-0856, ADR-0956): neither reference build (Schannel, OpenSSL) has <c>HTTPSRR</c>, so under
/// <c>--doh-url</c> an <c>https://</c> origin is resolved with the A and AAAA queries only, and no
/// HTTPS (type 65) query tells the DoH server the target port.
/// </summary>
[TestClass]
public sealed class Cgr157Tests
{
    private const int OriginPort = 21805;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginByDefault_AsksOnlyForTheAddresses()
    {
        var resolver = new RecordingHttpsOriginResolver();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());
        Diagnostics.Arrange("target", $"https://foo.test:{OriginPort}/x");

        _ = await connector.ConnectAsync(HttpsTarget(), CancellationToken.None);

        Diagnostics.Assert("look-ups", "addresses foo.test", string.Join(" | ", resolver.Calls));
        CollectionAssert.AreEqual(new[] { "addresses foo.test" }, resolver.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginWithHttpsRecordsOff_WritesNoHttpsRecordLine()
    {
        var resolver = new RecordingHttpsOriginResolver();
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()) { AsksHttpsRecords = false };
        Diagnostics.Arrange("AsksHttpsRecords", false);

        _ = await connector.ConnectAsync(HttpsTarget() with { Events = events }, CancellationToken.None);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("HTTPS-RR", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(new[] { "addresses foo.test" }, resolver.Calls);
    }

    private static ConnectTarget HttpsTarget() => new("foo.test", OriginPort, UseTls: true) { PoolScheme = "https" };

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
