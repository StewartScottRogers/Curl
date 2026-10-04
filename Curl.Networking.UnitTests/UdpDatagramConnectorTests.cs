using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives every branch of <see cref="UdpDatagramConnector" /> through fakes: resolve
/// failure (exit 6), open failure (exit 7), endpoint selection and cancellation.
/// </summary>
[TestClass]
public sealed class UdpDatagramConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_WithNullHost_ThrowsArgumentNullException()
    {
        var connector = CreateConnector(new FakeDnsResolver(), OpenFake([]));

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await connector.OpenAsync(null!, 69, CancellationToken.None));

        Assert.AreEqual("host", exception.ParamName);
    }

    [TestMethod]
    public async Task OpenAsync_WhenResolverReturnsNoAddresses_FailsWithCouldntResolveHostAndOpensNothing()
    {
        var resolver = new FakeDnsResolver();
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(resolver, OpenFake(opened));

        var result = await connector.OpenAsync("nonexistent.invalid", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
        Assert.IsNull(result.Channel);
        CollectionAssert.AreEqual(new[] { "nonexistent.invalid" }, resolver.ResolvedHosts);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task OpenAsync_ToAnOnionName_FailsWithExit6BeforeAnyLookUp()
    {
        // curl tftp://x.onion/f -> curl: (6) Not resolving .onion address (RFC 7686) (BL-1394).
        var resolver = new FakeDnsResolver(IPAddress.Loopback);
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(resolver, OpenFake(opened));

        var result = await connector.OpenAsync("x.onion", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Not resolving .onion address (RFC 7686)", result.ErrorMessage);
        Assert.IsEmpty(resolver.ResolvedHosts);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheResolverExplainsTheFailure_AddsTheReason()
    {
        // curl --dns-servers <nxdomain> tftp://bl694.example/x -> curl: (6) Could not resolve host: bl694.example (Domain name not found)
        var connector = CreateConnector(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.NotFound)), OpenFake([]));

        var result = await connector.OpenAsync("bl694.example", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: bl694.example (Domain name not found)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheDnsConfigurationDoesNotParse_FailsWithExit43()
    {
        var connector = CreateConnector(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.BadConfiguration)), OpenFake([]));

        var result = await connector.OpenAsync("bl694.example", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("Error 43 resolving bl694.example:69", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheExplainingResolverResolves_OpensItsAddress()
    {
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(new ReasoningDnsResolver(new DnsResolution([IPAddress.Loopback], DnsLookupFailure.None)), OpenFake(opened));

        await connector.OpenAsync("bl694.example", 69, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 69) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenOpenSucceeds_ReportsTheFirstResolvedAddressWithTheRequestedPort()
    {
        var first = IPAddress.Parse("192.0.2.1");
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(new FakeDnsResolver(first, IPAddress.Parse("192.0.2.2")), OpenFake(opened));

        var result = await connector.OpenAsync("tftp.example", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(result.Channel);
        Assert.AreEqual(new IPEndPoint(first, 6969), result.Channel.ServerEndPoint);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(first, 6969) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheFirstAddressCannotBeOpened_UsesTheNextAddress()
    {
        var first = IPAddress.Parse("2001:db8::1");
        var second = IPAddress.Parse("192.0.2.2");
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(
            new FakeDnsResolver(first, second),
            endPoint =>
            {
                opened.Add(endPoint);
                return endPoint.Address.Equals(first)
                    ? throw new SocketException((int)SocketError.AddressFamilyNotSupported)
                    : new FakeDatagramChannel(endPoint);
            });

        var result = await connector.OpenAsync("tftp.example", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        Assert.AreEqual(new IPEndPoint(second, 69), result.Channel.ServerEndPoint);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(first, 69), new IPEndPoint(second, 69) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenNoAddressCanBeOpened_FailsWithCouldntConnectAndElapsedMilliseconds()
    {
        var timeProvider = new ManualTimeProvider();
        var connector = new UdpDatagramConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            timeProvider,
            _ =>
            {
                timeProvider.Advance(3);
                throw new SocketException((int)SocketError.AddressFamilyNotSupported);
            });

        var result = await connector.OpenAsync("127.0.0.1", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:69 after 3 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsNull(result.Channel);
    }

    [TestMethod]
    public async Task OpenAsync_WhenResolveIsCancelled_ThrowsOperationCanceledException()
    {
        var connector = CreateConnector(
            new FakeDnsResolver { ExceptionToThrow = new OperationCanceledException() },
            OpenFake([]));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await connector.OpenAsync("tftp.example", 69, CancellationToken.None));
    }

    [TestMethod]
    public async Task OpenAsync_ThroughThePublicConstructor_OpensAUdpChannelToTheResolvedEndPoint()
    {
        var connector = new UdpDatagramConnector(new FakeDnsResolver(IPAddress.Loopback), TimeProvider.System);

        var result = await connector.OpenAsync("localhost", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        await using var channel = result.Channel;
        Assert.IsInstanceOfType<UdpDatagramChannel>(channel);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 69), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task OpenAsync_WithResolveEntry_OpensAtTheOverriddenAddressWithoutAskingTheResolver()
    {
        // curl -v --resolve tftp.test:6969:127.0.0.1 tftp://tftp.test:6969/x: "Trying 127.0.0.1:6969..." (curl 8.21.0, 2026-09-27).
        var resolver = new FakeDnsResolver(IPAddress.Parse("192.0.2.1"));
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(resolver, OpenFake(opened), ResolveOverrides.Parse(["tftp.test:6969:127.0.0.1"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 6969) }, opened);
        Assert.IsEmpty(resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task OpenAsync_WithConnectToMapping_ResolvesTheMappedHostAndOpensAtTheMappedPort()
    {
        // curl -v --connect-to tftp.test:6969:other: --resolve other:6969:127.0.0.1 tftp://tftp.test:6969/x:
        // "Trying 127.0.0.1:6969..." (curl 8.21.0, 2026-09-27).
        var resolver = new FakeDnsResolver(IPAddress.Parse("192.0.2.5"));
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(
            resolver,
            OpenFake(opened),
            connectToMappings: new ConnectToMappings(["tftp.test:6969:mapped.test:7000"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "mapped.test" }, resolver.ResolvedHosts);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("192.0.2.5"), 7000) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WithConnectToAndResolveForTheMappedHost_OpensAtTheMappedHostsOverriddenAddress()
    {
        // curl -v --connect-to tftp.test:6969:127.0.0.1:6970 --resolve tftp.test:6969:127.0.0.2 tftp://tftp.test:6969/x:
        // "Trying 127.0.0.1:6970..." - the entry for the URL's host no longer applies (curl 8.21.0, 2026-09-27).
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(
            new FakeDnsResolver(),
            OpenFake(opened),
            ResolveOverrides.Parse(["tftp.test:6969:192.0.2.1", "mapped.test:6970:192.0.2.9"]),
            new ConnectToMappings(["tftp.test:6969:mapped.test:6970"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Parse("192.0.2.9"), 6970) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheMappedHostDoesNotResolve_FailsWithCouldntResolveHostNamingTheMappedHost()
    {
        // curl --connect-to tftp.test:6969:nonexistent.invalid:70 tftp://tftp.test:6969/x:
        // "curl: (6) Could not resolve host: nonexistent.invalid" (curl 8.21.0, 2026-09-27).
        var connector = CreateConnector(
            new FakeDnsResolver(),
            OpenFake([]),
            connectToMappings: new ConnectToMappings(["tftp.test:6969:nonexistent.invalid:70"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_WhenNoMappedAddressCanBeOpened_FailsWithCouldntConnectNamingTheMappedDestination()
    {
        var connector = new UdpDatagramConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            new ManualTimeProvider(),
            _ => throw new SocketException((int)SocketError.AddressFamilyNotSupported),
            connectToMappings: new ConnectToMappings(["tftp.test:6969:mapped.test:7000"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(
            "Failed to connect to tftp.test:6969 via mapped.test:7000 after 0 ms: Could not connect to server",
            result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_WithUnparsableResolveEntry_FailsWithExit49AndResolvesNothing()
    {
        // curl --resolve bad tftp://tftp.test:6969/x: "curl: (49) Could not parse CURLOPT_RESOLVE entry 'bad'" (curl 8.21.0, 2026-09-27).
        var resolver = new FakeDnsResolver(IPAddress.Loopback);
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(resolver, OpenFake(opened), ResolveOverrides.Parse(["bad"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Could not parse CURLOPT_RESOLVE entry 'bad'", result.ErrorMessage);
        Assert.IsEmpty(resolver.ResolvedHosts);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task OpenAsync_WithUnparsableConnectToDestination_FailsWithExit49AndResolvesNothing()
    {
        // curl --connect-to tftp.test:6969:[::1:9 tftp://tftp.test:6969/x:
        // "curl: (49) Invalid IPv6 address format in '[::1:9'" (curl 8.21.0, 2026-09-27).
        var resolver = new FakeDnsResolver(IPAddress.Loopback);
        var connector = CreateConnector(resolver, OpenFake([]), connectToMappings: new ConnectToMappings(["tftp.test:6969:[::1:9"]));

        var result = await connector.OpenAsync("tftp.test", 6969, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Invalid IPv6 address format in '[::1:9'", result.ErrorMessage);
        Assert.IsEmpty(resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task OpenAsync_ThroughThePublicConstructorWithOverrides_OpensAUdpChannelAtTheMappedOverriddenEndPoint()
    {
        var connector = new UdpDatagramConnector(
            new FakeDnsResolver(),
            TimeProvider.System,
            ResolveOverrides.Parse(["mapped.test:7000:127.0.0.1"]),
            new ConnectToMappings(["tftp.test:69:mapped.test:7000"]));

        var result = await connector.OpenAsync("tftp.test", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        await using var channel = result.Channel;
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 7000), channel.ServerEndPoint);
    }

    [TestMethod]
    public async Task OpenAsync_UnderIPv4ToANameWithBothFamilies_OpensItsIPv4Address()
    {
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback, IPAddress.Loopback), OpenFake(opened), addressFamily: AddressFamily.InterNetwork);

        var result = await connector.OpenAsync("tftp.example", 69, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 69) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_UnderIPv6WithAResolveEntryOfOnlyIPv4_FailsWithCouldntResolveHostAndOpensNothing()
    {
        // curl -6 -v --resolve foo:47501:127.0.0.1 tftp://foo:47501/x -> curl: (6) Could not resolve host: foo
        // (curl 8.21.0, 2026-09-28, BL-500).
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(
            new FakeDnsResolver(IPAddress.Loopback), OpenFake(opened), ResolveOverrides.Parse(["foo:47501:127.0.0.1"]), addressFamily: AddressFamily.InterNetworkV6);

        var result = await connector.OpenAsync("foo", 47501, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: foo", result.ErrorMessage);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral()
    {
        // curl -6 -v tftp://127.0.0.1:47501/x -> *   Trying 127.0.0.1:47501... (curl 8.21.0, BL-500).
        var opened = new List<IPEndPoint>();
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.Loopback), OpenFake(opened), addressFamily: AddressFamily.InterNetworkV6);

        var result = await connector.OpenAsync("127.0.0.1", 47501, CancellationToken.None);

        Assert.IsNotNull(result.Channel);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 47501) }, opened);
    }

    [TestMethod]
    public async Task OpenAsync_WithADiagnosticLog_LogsTheResolveAndTheChannelAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new UdpDatagramConnector(new FakeDnsResolver(IPAddress.Loopback), new ManualTimeProvider(), OpenFake([]), diagnosticLog: log);

        await connector.OpenAsync("tftp.test", 69, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "tftp.test:69 resolved by lookup to 127.0.0.1 in 0 ms" }, log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns));
        CollectionAssert.AreEqual(new[] { "UDP channel open to 127.0.0.1:69" }, log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Connect));
    }

    [TestMethod]
    public async Task OpenAsync_WhenAResolveEntryAnswers_LogsTheResolveAsFromTheDnsCache()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new UdpDatagramConnector(
            new FakeDnsResolver(), new ManualTimeProvider(), OpenFake([]), ResolveOverrides.Parse(["tftp.test:69:127.0.0.1"]), diagnosticLog: log);

        await connector.OpenAsync("tftp.test", 69, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "tftp.test:69 resolved from the DNS cache to 127.0.0.1 in 0 ms" }, log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns));
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheNameDoesNotResolve_LogsTheFailureAtErrorForDns()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new UdpDatagramConnector(new FakeDnsResolver(), new ManualTimeProvider(), OpenFake([]), diagnosticLog: log);

        await connector.OpenAsync("nonexistent.invalid", 69, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "failed with CouldntResolveHost (6): Could not resolve host: nonexistent.invalid" },
            log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Dns));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns));
    }

    [TestMethod]
    public async Task OpenAsync_WhenNoChannelOpens_LogsTheFailureAtErrorForConnect()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var connector = new UdpDatagramConnector(
            new FakeDnsResolver(IPAddress.Loopback), new ManualTimeProvider(), _ => throw new SocketException((int)SocketError.AddressFamilyNotSupported), diagnosticLog: log);

        await connector.OpenAsync("tftp.test", 69, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "failed with CouldntConnect (7): Failed to connect to tftp.test:69 after 0 ms: Could not connect to server" },
            log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Connect));
    }

    private static UdpDatagramConnector CreateConnector(
        IDnsResolver resolver,
        Func<IPEndPoint, IDatagramChannel> openChannel,
        ResolveOverrides? resolveOverrides = null,
        ConnectToMappings? connectToMappings = null,
        AddressFamily addressFamily = AddressFamily.Unspecified) =>
        new(resolver, new ManualTimeProvider(), openChannel, resolveOverrides, connectToMappings, addressFamily);

    private static Func<IPEndPoint, IDatagramChannel> OpenFake(List<IPEndPoint> opened) =>
        endPoint =>
        {
            opened.Add(endPoint);
            return new FakeDatagramChannel(endPoint);
        };
}
