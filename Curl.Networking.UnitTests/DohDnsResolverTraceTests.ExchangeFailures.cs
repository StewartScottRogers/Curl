using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// Pins the --trace-config doh lines for a DoH query whose connection or exchange failed (BL-850, BL-2970).
public sealed partial class DohDnsResolverTraceTests
{
    [TestMethod]
    public async Task ResolveAsync_ABodyCutShort_ReportsTheReceiveFailureForEachQuery()
    {
        // A close-delimited body is read to the close (CGR-160, BL-3689); one cut short of its Content-Length is not.
        var lines = await TraceAsync([.. Response("HTTP/1.1 200 OK\r\nContent-Length: 1000\r\n\r\n"), .. Convert.FromHexString(MeasuredAAnswer)]);

        AssertLines(
            ["[DoH] [DoH] [AAAA] error: Failure when receiving data from the peer", "[DoH] [DoH] [A] error: Failure when receiving data from the peer"],
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
            ["[DoH] [DoH] [AAAA] error: SSL peer certificate or SSH remote key was not OK", "[DoH] [DoH] [A] error: SSL peer certificate or SSH remote key was not OK"],
            lines);
    }

    [TestMethod]
    public async Task ResolveAsync_A500WithAnEmptyBody_ReportsTheHttpStatusAndResolvesNothing()
    {
        // Measured with curl 8.22.0 (Schannel) under -4: the status line, then exit 6 (BL-2972).
        var (lines, addresses) = await TraceOnlyAAsync(Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"));

        AssertLines(["[DoH] [DoH] [A] error: HTTP status 500"], lines);
        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 301 Moved Permanently", "[DoH] [DoH] [AAAA] error: HTTP status 301", "[DoH] [DoH] [A] error: HTTP status 301")]
    [DataRow("HTTP/1.1 199 Early", "[DoH] [DoH] [AAAA] error: HTTP status 199", "[DoH] [DoH] [A] error: HTTP status 199")]
    public async Task ResolveAsync_AStatusOutside2xxWithAnAnswerBody_ReportsTheHttpStatusWithoutDecoding(string statusLine, string aaaaLine, string aLine)
    {
        // doh_probe_done (curl-8_22_0) fails any status below 200 or from 300 before doh_resp_decode.
        byte[] answer = Convert.FromHexString(MeasuredAAnswer);
        var lines = await TraceAsync([.. Response($"{statusLine}\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer]);

        AssertLines([aaaaLine, aLine], lines);
    }

    [TestMethod]
    public async Task ResolveAsync_OneExchangeFailingAndOneDecoding_ReportsTheFailureAndResolvesTheDecodedAddress()
    {
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Ok(Convert.FromHexString(MeasuredAAnswer)));
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\n"));

        var (lines, addresses) = await ResolveAsync(connector, System.Net.Sockets.AddressFamily.Unspecified);

        AssertLines(["[DoH] [DoH] [AAAA] error: Failure when receiving data from the peer"], lines);
        Assert.AreEqual("127.0.0.1", addresses.Single());
    }
}
