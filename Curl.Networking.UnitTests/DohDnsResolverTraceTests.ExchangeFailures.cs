using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

// Pins the --trace-config doh lines for a DoH query whose connection or exchange failed (BL-850).
public sealed partial class DohDnsResolverTraceTests
{
    [TestMethod]
    public async Task ResolveAsync_A500WithAnEmptyBody_ReportsTooSmallForBothTypes()
    {
        var lines = await TraceAsync(Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"));

        var expectedLines = new[]
            {
                "[DNS] DoH: Too small type A for example.test",
                "[DNS] DoH: Too small type AAAA for example.test",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ACloseDelimitedBody_ReportsTheReceiveFailuresAndAnEmptyEntry()
    {
        // curl skips the decode of a failed exchange, so it counts as answered and the entry,
        // with no address and the TTL it starts from, is printed.
        var lines = await TraceAsync([.. Response("HTTP/1.1 200 OK\r\n\r\n"), .. Convert.FromHexString(MeasuredAAnswer)]);

        var expectedLines = new[]
            {
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 2147483647 seconds",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ARefusedCertificate_ReportsTheConnectFailuresAndAnEmptyEntry()
    {
        // Without --doh-insecure: both handshakes failed with exit 60.
        var connector = new FakeConnector
        {
            Failure = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325)"),
        };

        var lines = await TraceAsync(connector);

        var expectedLines = new[]
            {
                "[DNS] DoH request SSL peer certificate or SSH remote key was not OK",
                "[DNS] DoH request SSL peer certificate or SSH remote key was not OK",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 2147483647 seconds",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }

    [TestMethod]
    public async Task ResolveAsync_OneExchangeFailingAndOneDecoding_ReportsBothWithTheDecodedAddress()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\n\r\n"));

        var lines = await TraceAsync(connector);

        var expectedLines = new[]
            {
                "[DNS] DoH request Failure when receiving data from the peer",
                "[DNS] hostname: example.test",
                "[DoH] TTL: 60 seconds",
                "[DoH] A: 127.0.0.1",
            };
        Diagnostics.Diff("trace lines", string.Join("\n", expectedLines), string.Join("\n", lines));
        CollectionAssert.AreEqual(expectedLines, lines);
    }
}
