using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// Pins the --trace-config doh lines for a DoH query whose connection or exchange failed (BL-850, BL-2970).
public sealed partial class DohDnsResolverTraceTests
{
    [TestMethod]
    public async Task ResolveAsync_ACloseDelimitedBody_ReportsTheReceiveFailureForEachQuery()
    {
        var lines = await TraceAsync([.. Response("HTTP/1.1 200 OK\r\n\r\n"), .. Convert.FromHexString(MeasuredAAnswer)]);

        AssertLines(
            ["[DoH] [DoH] [A] error: Failure when receiving data from the peer", "[DoH] [DoH] [AAAA] error: Failure when receiving data from the peer"],
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_ARefusedCertificate_ReportsTheConnectFailureForEachQuery()
    {
        // Without --doh-insecure: both handshakes failed with exit 60.
        var connector = new FakeConnector
        {
            Failure = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325)"),
        };

        var lines = await TraceAsync(connector);

        AssertLines(
            ["[DoH] [DoH] [A] error: SSL peer certificate or SSH remote key was not OK", "[DoH] [DoH] [AAAA] error: SSL peer certificate or SSH remote key was not OK"],
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_OneExchangeFailingAndOneDecoding_ReportsTheFailureAndResolvesTheDecodedAddress()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\n\r\n"));

        var (lines, addresses) = await ResolveAsync(connector, System.Net.Sockets.AddressFamily.Unspecified);

        AssertLines(["[DoH] [DoH] [AAAA] error: Failure when receiving data from the peer"], lines);
        Assert.AreEqual("127.0.0.1", addresses.Single());
    }
}
