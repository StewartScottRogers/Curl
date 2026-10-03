using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesSetupFilter" />, curl 8.21.0's <c>[SETUP]</c> filter lines
/// under <c>-vv</c> and <c>--trace-config setup</c>, alone and beside the <c>[DNS]</c> lines
/// (measured, BL-1103 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilter_WritesCurlsSetupLinesAroundTheConnect()
    {
        // curl -s -vv http://127.0.0.1:47320/ (BL-1103 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesSetupFilter = true,
        };

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47320, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to origin 127.0.0.1:47320",
                "  Trying 127.0.0.1:47320...",
                "opened",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupAndDnsFilters_NestsTheDnsLinesInsideTheSetupLines()
    {
        // curl -s -v --trace-config setup,dns (the [SETUP] and [DNS] lines of -vvvv, BL-1103 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesSetupFilter = true,
            TracesDnsFilter = true,
        };

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47313, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[DNS] created DNS filter for 127.0.0.1:47313, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:47313",
                "[SETUP] happy eyeballing to origin 127.0.0.1:47313",
                "  Trying 127.0.0.1:47313...",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "opened",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterWhenTheDialIsRefused_WritesNoRemovalLines()
    {
        // curl -s -v --trace-config setup http://127.0.0.1:1/: added and happy eyeballing only.
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesSetupFilter = true };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "[SETUP] added", "[SETUP] happy eyeballing to origin 127.0.0.1:1" },
            events.Calls.Where(line => line.StartsWith("[SETUP]", StringComparison.Ordinal)).ToArray());
    }
}
