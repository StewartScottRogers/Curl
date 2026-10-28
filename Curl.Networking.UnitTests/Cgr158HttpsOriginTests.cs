using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-158 for an <c>https://</c> origin (BL-3915): a host name the DoH resolver cannot put in a query is
/// curl 8.22.0's exit 27 <c>Failed to encode DoH packet [N]</c> with nothing sent, not exit 6.
/// </summary>
[TestClass]
public sealed class Cgr158HttpsOriginTests
{
    [TestMethod]
    [DataRow("label64", "Failed to encode DoH packet [1]")]
    [DataRow("emptylabel", "Failed to encode DoH packet [1]")]
    [DataRow("name310", "Failed to encode DoH packet [13]")]
    public async Task ConnectAsync_HttpsOriginNameThatCannotBeEncodedForDoh_IsExit27AndSendsNothing(string name, string expectedMessage)
    {
        var dohConnector = new FakeConnector();
        var resolver = new DohDnsResolver(dohConnector, new Uri("http://127.0.0.1:21800/dns-query"));
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()) { AsksHttpsRecords = true };
        var target = new ConnectTarget(HostNamed(name), 21802, UseTls: true) { PoolScheme = "https" };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OutOfMemory, result.ExitCode);
        Assert.AreEqual(expectedMessage, result.ErrorMessage);
        Assert.IsEmpty(dohConnector.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_HttpsOriginAnotherResolverFindsNoAddress_IsStillExit6()
    {
        var connector = new TcpConnector(new EmptyHttpsOriginResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()) { AsksHttpsRecords = true };
        var target = new ConnectTarget("foo..test", 21802, UseTls: true) { PoolScheme = "https" };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_HttpsOriginDohFindsNoAddressForAnEncodableName_IsStillExit6()
    {
        var resolver = new DohDnsResolver(new FakeConnector(), new Uri("http://127.0.0.1:21800/dns-query"));
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()) { AsksHttpsRecords = true };
        var target = new ConnectTarget("foo.test", 21802, UseTls: true) { PoolScheme = "https" };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
    }

    private sealed class EmptyHttpsOriginResolver : IHttpsOriginDnsResolver
    {
        public ValueTask<IReadOnlyList<System.Net.IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<System.Net.IPAddress>>([]);

        public ValueTask<IReadOnlyList<System.Net.IPAddress>> ResolveHttpsOriginAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<System.Net.IPAddress>>([]);

        public ServiceBindingRecord? HttpsRecordFor(string host, int port) => null;
    }

    private static string HostNamed(string name) => name switch
    {
        "label64" => new string('a', 64) + ".test",
        "emptylabel" => "foo..test",
        _ => string.Concat(Enumerable.Repeat(new string('b', 50) + ".", 6)) + "test",
    };
}
