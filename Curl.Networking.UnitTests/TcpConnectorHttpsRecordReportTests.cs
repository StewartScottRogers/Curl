using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins where <see cref="TcpConnector" /> writes curl 8.22.0's <c>-v</c>
/// <c>HTTPS-RR &lt;host&gt;:&lt;port&gt;:</c> line after an <c>https://</c> origin's HTTPS-RR resolve:
/// just before <c>Host &lt;host&gt;:&lt;port&gt; was resolved.</c>, since <c>cf-dns.c</c>'s HTTPS
/// resolve filter sits above the address one (ADR-0727's BL-2980 amendment).
/// </summary>
[TestClass]
public sealed class TcpConnectorHttpsRecordReportTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginWithARecord_WritesTheRecordLineBeforeHostWasResolved()
    {
        var resolver = new HttpsRecordResolver(new ServiceBindingRecord(1, ".") { ApplicationProtocols = ["h2"] });
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("record", "1 . alpn=h2");

        _ = await Connector(resolver).ConnectAsync(Target("foo.example.com", events), CancellationToken.None);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(
            new[] { "HTTPS-RR foo.example.com:443: 1 . alpn=h2", "Host foo.example.com:443 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1" },
            events.Info.Take(4).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginWithNoRecord_WritesADashBeforeHostWasResolved()
    {
        var resolver = new HttpsRecordResolver(null);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("record", "(none)");

        _ = await Connector(resolver).ConnectAsync(Target("foo.example.com", events), CancellationToken.None);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(
            new[] { "HTTPS-RR foo.example.com:443: -", "Host foo.example.com:443 was resolved." },
            events.Info.Take(2).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginWithARecordForAnotherTarget_WritesNotApplicable()
    {
        var resolver = new HttpsRecordResolver(new ServiceBindingRecord(1, "alt.example.com"));
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("record", "1 alt.example.com.");

        _ = await Connector(resolver).ConnectAsync(Target("foo.example.com", events), CancellationToken.None);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Assert.AreEqual("HTTPS-RR foo.example.com:443: not applicable", events.Info.FirstOrDefault());
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginNamedByAnAddress_WritesNoRecordLine()
    {
        var resolver = new HttpsRecordResolver(null);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("origin", "https://127.0.0.1/");

        _ = await Connector(resolver).ConnectAsync(Target("127.0.0.1", events), CancellationToken.None);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("HTTPS-RR", StringComparison.Ordinal)));
    }

    private static TcpConnector Connector(IDnsResolver resolver) =>
        new(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

    private static ConnectTarget Target(string host, ITransferEvents events) =>
        new(host, 443, UseTls: true) { PoolScheme = "https", Events = events };

    // Answers 127.0.0.1 and gives one HTTPS record for every host and port.
    private sealed class HttpsRecordResolver(ServiceBindingRecord? record) : IHttpsOriginDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);

        public ValueTask<IReadOnlyList<IPAddress>> ResolveHttpsOriginAsync(string host, int port, CancellationToken cancellationToken) =>
            ResolveAsync(host, cancellationToken);

        public ServiceBindingRecord? HttpsRecordFor(string host, int port) => record;
    }
}
