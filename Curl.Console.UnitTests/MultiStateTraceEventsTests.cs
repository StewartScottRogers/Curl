using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="MultiStateTraceEvents" />: curl 8.21.0's <c>[MULTI]</c> lines of a plain HTTP transfer, each
/// group beside the line or event curl writes it next to, whichever other components are traced, with every
/// event passed on unchanged (measured, BL-1188 Notes).
/// </summary>
[TestClass]
public sealed class MultiStateTraceEventsTests
{
    private static readonly ConnectionOpenedEvent Opened = new()
    {
        HostName = "127.0.0.1",
        RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 47821),
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 64916),
        ConnectionNumber = 0,
    };

    [TestMethod]
    public void StartLines_AreCurlsLinesUpToConnect()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "[MULTI] [INIT] added to multi, mid=1, running=1, total=2",
                "[MULTI] [INIT] pollset[], timeouts=0, paused 0/0 (r/w)",
                "[MULTI] [INIT] multi_wait(fds=0, timeout=0) tinternal=0",
                "[MULTI] [INIT] -> [SETUP]",
                "[MULTI] [SETUP] [PGRS-STARTOP] set",
                "[MULTI] [SETUP] [PGRS-STARTSINGLE] set",
                "[MULTI] [SETUP] -> [CONNECT]",
            },
            MultiStateTraceEvents.StartLines.ToArray());
    }

    [TestMethod]
    public void TheMeasuredPlainTransferUnderMultiAlone_WritesCurlsLinesInCurlsOrder()
    {
        // curl 8.21.0, -s -v --trace-config multi, a 200 with Content-Length: 2 and "hi" (measured 2026-10-02, BL-1188 Notes).
        CallRecordingEvents inner = new();
        MicrosecondClock clock = new();
        MultiStateTraceEvents events = new(inner, clock, () => 0);

        clock.Microseconds = 59;
        events.ReportInfo("  Trying 127.0.0.1:47821...");
        clock.Microseconds = 633;
        events.ReportConnectionOpened(Opened);
        events.ReportInfo("using HTTP/1.x");
        events.ReportRequestHeader("GET / HTTP/1.1\r\n"u8);
        clock.Microseconds = 732;
        events.ReportInfo("Request completely sent off");
        clock.Microseconds = 30768;
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportResponseHeader("Content-Length: 2\r\n"u8);
        events.ReportDataReceived("hi"u8);
        events.ReportInfo("Connection #0 to host 127.0.0.1:47821 left intact");

        CollectionAssert.AreEqual(
            new[]
            {
                "Info [MULTI] [CONNECT] transfer credentials: -",
                "Info [MULTI] [CONNECT] [CPOOL] added connection 0. The cache now contains 1 members",
                "Info [MULTI] [CONNECT] [PGRS-POSTQUEUE] set",
                "Info [MULTI] [CONNECT] Curl_conn_setup() -> 0",
                "Info [MULTI] [CONNECT] -> [CONNECTING]",
                "Info [MULTI] [CONNECTING] [PGRS-NAMELOOKUP] added 59ns",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP]",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info   Trying 127.0.0.1:47821...",
                "Info [MULTI] [CONNECTING] pollset[fd=3 OUT], timeouts=0",
                "Info [MULTI] [CONNECTING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info [MULTI] [CONNECTING] [PGRS-CONNECT] added 633ns",
                "Opened",
                "Info [MULTI] [CONNECTING] connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]",
                "Info [MULTI] [CONNECTING] reduced to [0][TCP]",
                "Info [MULTI] [CONNECTING] -> [PROTOCONNECT]",
                "Info [MULTI] [PROTOCONNECT] -> [DO]",
                "Info using HTTP/1.x",
                "Info [MULTI] [DO] xfer_setup: recv_idx=0, send_idx=0",
                "RequestHeader 16",
                "Info Request completely sent off",
                "Info [MULTI] [DO] -> [DID]",
                "Info [MULTI] [DID] [PGRS-PRETRANSFER] added 732ns",
                "Info [MULTI] [DID] [PGRS-POSTRANSFER] added 732ns",
                "Info [MULTI] [DID] -> [PERFORMING]",
                "Info [MULTI] [PERFORMING] pollset[fd=3 IN], timeouts=0",
                "Info [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "ResponseHeader 17",
                "Info [MULTI] [PERFORMING] [PGRS-STARTTRANSFER] added 30768ns",
                "ResponseHeader 19",
                "DataReceived 2",
                "Info [MULTI] [PERFORMING] -> [DONE]",
                "Info [MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0",
                "Info [MULTI] [DONE] multi_done_locked, in use=0",
                "Info Connection #0 to host 127.0.0.1:47821 left intact",
                "Info [MULTI] [DONE] -> [COMPLETED]",
                "Info [MULTI] [COMPLETED] -> [MSGSENT]",
                "Info [MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1",
            },
            inner.Calls);
    }

    [TestMethod]
    public void TheMeasuredPlainTransferUnderAll_PutsEachGroupWhereCurlPutsItAmongTheOtherComponents()
    {
        // curl 8.21.0, -s -v --trace-config all, the same exchange (measured 2026-10-02, BL-1188 Notes); the
        // [TCP] send and recv lines Curl does not write are left out.
        CallRecordingEvents inner = new();
        MultiStateTraceEvents events = new(inner, new MicrosecondClock(), () => 0);

        foreach (string line in (string[])
        [
            "[SETUP] added", "[DNS] created DNS filter for 127.0.0.1:47823, transport=3, queries=3", "[DNS] added",
            "[DNS] cf_dns_start host 127.0.0.1:47823", "[SETUP] happy eyeballing to origin 127.0.0.1:47823",
            "[HAPPY-EYEBALLS] init ip ballers for transport 3", "  Trying 127.0.0.1:47823...",
            "[HAPPY-EYEBALLS] adjust_pollset -> 0, 1 socks", "[TCP] connected on fd=436",
        ])
        {
            events.ReportInfo(line);
        }

        events.ReportConnectionOpened(Opened);
        events.ReportInfo("[DNS] destroy");
        events.ReportInfo("[TCP] query ALPN");
        events.ReportInfo("using HTTP/1.x");
        events.ReportInfo("Request completely sent off");
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportInfo("[WRITE] [OUT] done");
        events.ReportInfo("[READ] client_reset, clear readers");
        events.ReportInfo("Connection #0 to host 127.0.0.1:47823 left intact");

        CollectionAssert.AreEqual(
            new[]
            {
                "Info [MULTI] [CONNECT] transfer credentials: -",
                "Info [MULTI] [CONNECT] [CPOOL] added connection 0. The cache now contains 1 members",
                "Info [MULTI] [CONNECT] [PGRS-POSTQUEUE] set",
                "Info [SETUP] added",
                "Info [DNS] created DNS filter for 127.0.0.1:47823, transport=3, queries=3",
                "Info [DNS] added",
                "Info [MULTI] [CONNECT] Curl_conn_setup() -> 0",
                "Info [MULTI] [CONNECT] -> [CONNECTING]",
                "Info [DNS] cf_dns_start host 127.0.0.1:47823",
                "Info [MULTI] [CONNECTING] [PGRS-NAMELOOKUP] added 0ns",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP]",
                "Info [SETUP] happy eyeballing to origin 127.0.0.1:47823",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info [HAPPY-EYEBALLS] init ip ballers for transport 3",
                "Info   Trying 127.0.0.1:47823...",
                "Info [HAPPY-EYEBALLS] adjust_pollset -> 0, 1 socks",
                "Info [MULTI] [CONNECTING] pollset[fd=436 OUT], timeouts=0",
                "Info [MULTI] [CONNECTING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info [TCP] connected on fd=436",
                "Info [MULTI] [CONNECTING] [PGRS-CONNECT] added 0ns",
                "Opened",
                "Info [MULTI] [CONNECTING] connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]",
                "Info [DNS] destroy",
                "Info [MULTI] [CONNECTING] reduced to [0][TCP]",
                "Info [MULTI] [CONNECTING] -> [PROTOCONNECT]",
                "Info [MULTI] [PROTOCONNECT] -> [DO]",
                "Info [TCP] query ALPN",
                "Info using HTTP/1.x",
                "Info [MULTI] [DO] xfer_setup: recv_idx=0, send_idx=0",
                "Info Request completely sent off",
                "Info [MULTI] [DO] -> [DID]",
                "Info [MULTI] [DID] [PGRS-PRETRANSFER] added 0ns",
                "Info [MULTI] [DID] [PGRS-POSTRANSFER] added 0ns",
                "Info [MULTI] [DID] -> [PERFORMING]",
                "Info [MULTI] [PERFORMING] pollset[fd=436 IN], timeouts=0",
                "Info [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "ResponseHeader 17",
                "Info [MULTI] [PERFORMING] [PGRS-STARTTRANSFER] added 0ns",
                "Info [MULTI] [PERFORMING] -> [DONE]",
                "Info [MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0",
                "Info [WRITE] [OUT] done",
                "Info [READ] client_reset, clear readers",
                "Info [MULTI] [DONE] multi_done_locked, in use=0",
                "Info Connection #0 to host 127.0.0.1:47823 left intact",
                "Info [MULTI] [DONE] -> [COMPLETED]",
                "Info [MULTI] [COMPLETED] -> [MSGSENT]",
                "Info [MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1",
            },
            inner.Calls);
    }

    [TestMethod]
    public void TheMeasuredReusedConnection_WritesCurlsLinesAndNoConnectGroupAndTakesNoConnectionNumber()
    {
        // curl 8.21.0, -s -v --trace-config multi, the second of two URLs on one kept-alive connection
        // (measured 2026-10-02, BL-1212 Notes).
        CallRecordingEvents inner = new();
        MicrosecondClock clock = new();
        int connectionNumbersTaken = 0;
        MultiStateTraceEvents events = new(inner, clock, () => connectionNumbersTaken++);

        events.ReportConnectionReused(null!);
        events.ReportRequestHeader("GET /b HTTP/1.1\r\n"u8);
        clock.Microseconds = 229;
        events.ReportInfo("Request completely sent off");
        clock.Microseconds = 4002;
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportInfo("Connection #0 to host 127.0.0.1:18712 left intact");

        Assert.AreEqual(0, connectionNumbersTaken);
        CollectionAssert.AreEqual(
            new[]
            {
                "Info [MULTI] [CONNECT] transfer credentials: -",
                "Reused",
                "Info [MULTI] [CONNECT] [PGRS-POSTQUEUE] set",
                "Info [MULTI] [CONNECT] -> [CONNECTING]",
                "Info [MULTI] [CONNECTING] -> [PROTOCONNECT]",
                "Info [MULTI] [PROTOCONNECT] -> [DO]",
                "Info [MULTI] [DO] xfer_setup: recv_idx=0, send_idx=0",
                "RequestHeader 17",
                "Info Request completely sent off",
                "Info [MULTI] [DO] -> [DID]",
                "Info [MULTI] [DID] [PGRS-PRETRANSFER] added 229ns",
                "Info [MULTI] [DID] [PGRS-POSTRANSFER] added 229ns",
                "Info [MULTI] [DID] -> [PERFORMING]",
                "Info [MULTI] [PERFORMING] pollset[fd=3 IN], timeouts=0",
                "Info [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "ResponseHeader 17",
                "Info [MULTI] [PERFORMING] [PGRS-STARTTRANSFER] added 4002ns",
                "Info [MULTI] [PERFORMING] -> [DONE]",
                "Info [MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0",
                "Info [MULTI] [DONE] multi_done_locked, in use=0",
                "Info Connection #0 to host 127.0.0.1:18712 left intact",
                "Info [MULTI] [DONE] -> [COMPLETED]",
                "Info [MULTI] [COMPLETED] -> [MSGSENT]",
                "Info [MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1",
            },
            inner.Calls);
    }

    [TestMethod]
    public void TheMeasuredRefusedConnect_WritesCurlsFailedConnectAndClosingLinesInCurlsOrder()
    {
        // curl 8.21.0, -s -v --trace-config multi, http://127.0.0.1:1/, exit 7 (measured 2026-10-02, BL-1212
        // Notes); curl polls once per second of the connect, Curl once (ADR-0391).
        CallRecordingEvents inner = new();
        MicrosecondClock clock = new();
        MultiStateTraceEvents events = new(inner, clock, () => 4);

        clock.Microseconds = 69;
        events.ReportInfo("  Trying 127.0.0.1:1...");
        events.ReportInfo("connect to 127.0.0.1 port 1 from 0.0.0.0 port 0 failed: Connection refused");
        clock.Microseconds = 2024209;
        events.ReportInfo("Failed to connect to 127.0.0.1:1 after 2024 ms: Could not connect to server");
        events.ReportInfo("closing connection #4");

        CollectionAssert.AreEqual(
            new[]
            {
                "Info [MULTI] [CONNECT] transfer credentials: -",
                "Info [MULTI] [CONNECT] [CPOOL] added connection 4. The cache now contains 1 members",
                "Info [MULTI] [CONNECT] [PGRS-POSTQUEUE] set",
                "Info [MULTI] [CONNECT] Curl_conn_setup() -> 0",
                "Info [MULTI] [CONNECT] -> [CONNECTING]",
                "Info [MULTI] [CONNECTING] [PGRS-NAMELOOKUP] added 69ns",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP]",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info   Trying 127.0.0.1:1...",
                "Info [MULTI] [CONNECTING] pollset[fd=3 OUT], timeouts=0",
                "Info [MULTI] [CONNECTING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "Info [MULTI] [CONNECTING] cf_setup_connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info [MULTI] [CONNECTING] Curl_multi_will_close fd=3",
                "Info connect to 127.0.0.1 port 1 from 0.0.0.0 port 0 failed: Connection refused",
                "Info Failed to connect to 127.0.0.1:1 after 2024 ms: Could not connect to server",
                "Info [MULTI] [CONNECTING] failed to connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]",
                "Info [MULTI] [CONNECTING] connect failed -> 7",
                "Info [MULTI] [CONNECTING] multi_done: status: 7 prem: 1 done: 0",
                "Info [MULTI] [CONNECTING] multi_done_locked, in use=0",
                "Info [MULTI] [CONNECTING] multi_done, terminating conn #4 to 127.0.0.1:1, forbid=0, close=0, premature=1, conn_multiplex=0",
                "Info closing connection #4",
                "Info [MULTI] [CONNECTING] -> [COMPLETED]",
                "Info [MULTI] [COMPLETED] [PGRS-PRETRANSFER] added 2024209ns",
                "Info [MULTI] [COMPLETED] [PGRS-POSTRANSFER] added 2024209ns",
                "Info [MULTI] [COMPLETED] [PGRS-STARTTRANSFER] added 2024209ns",
                "Info [MULTI] [COMPLETED] -> [MSGSENT]",
                "Info [MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1",
            },
            inner.Calls);
    }

    [TestMethod]
    public void AFailedAddressBeforeOneThatConnects_WritesItsCloseAndTheConnectedLinesAfter()
    {
        CallRecordingEvents inner = new();
        MultiStateTraceEvents events = new(inner, new MicrosecondClock(), () => 0);
        events.ReportInfo("connect to ::1 port 80 from :: port 0 failed: Connection refused");
        inner.Calls.Clear();

        events.ReportInfo("connect to 127.0.0.2 port 80 from 0.0.0.0 port 0 failed: Connection refused");
        events.ReportConnectionOpened(Opened);

        CollectionAssert.AreEqual(
            new[]
            {
                "Info [MULTI] [CONNECTING] Curl_multi_will_close fd=3",
                "Info connect to 127.0.0.2 port 80 from 0.0.0.0 port 0 failed: Connection refused",
                "Info [MULTI] [CONNECTING] [PGRS-CONNECT] added 0ns",
                "Opened",
                "Info [MULTI] [CONNECTING] connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ASecondFailedToConnectLine_WritesNoFailedConnectLinesAgain()
    {
        CallRecordingEvents inner = new();
        MultiStateTraceEvents events = new(inner, new MicrosecondClock(), () => 0);
        events.ReportInfo("Failed to connect to h:1 after 1 ms: Could not connect to server");
        inner.Calls.Clear();

        events.ReportInfo("Failed to connect to h:1 after 1 ms: Could not connect to server");

        CollectionAssert.AreEqual(new[] { "Info Failed to connect to h:1 after 1 ms: Could not connect to server" }, inner.Calls);
    }

    [TestMethod]
    public void AShuttingDownLine_EndsTheTransferAsLeftIntactDoes()
    {
        CallRecordingEvents inner = new();
        MultiStateTraceEvents events = new(inner, new MicrosecondClock(), () => 0);
        events.ReportInfo("Request completely sent off");
        inner.Calls.Clear();

        events.ReportInfo("shutting down connection #0");

        CollectionAssert.AreEqual(
            new[]
            {
                "Info [MULTI] [PERFORMING] pollset[fd=3 IN], timeouts=0",
                "Info [MULTI] [PERFORMING] multi_wait(fds=1, timeout=1000) tinternal=-1",
                "Info [MULTI] [PERFORMING] [PGRS-STARTTRANSFER] added 0ns",
                "Info [MULTI] [PERFORMING] -> [DONE]",
                "Info [MULTI] [DONE] multi_done: status: 0 prem: 0 done: 0",
                "Info [MULTI] [DONE] multi_done_locked, in use=0",
                "Info shutting down connection #0",
                "Info [MULTI] [DONE] -> [COMPLETED]",
                "Info [MULTI] [COMPLETED] -> [MSGSENT]",
                "Info [MULTI] [COMPLETED] removed from multi, mid=1, running=0, total=1",
            },
            inner.Calls);
    }

    [TestMethod]
    [DataRow("Connection #0 to host h:80 was closed")]
    [DataRow("A line no group is tied to")]
    public void ALineNoGroupIsTiedTo_IsPassedOnAlone(string line)
    {
        CallRecordingEvents inner = new();
        MultiStateTraceEvents events = new(inner, new MicrosecondClock(), () => 0);

        events.ReportInfo(line);

        CollectionAssert.AreEqual(new[] { $"Info {line}" }, inner.Calls);
    }

    [TestMethod]
    public void TheEventsNoGroupIsTiedTo_ArePassedOnUnchanged()
    {
        CallRecordingEvents inner = new();
        MultiStateTraceEvents events = new(inner, new MicrosecondClock(), () => 0);

        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportCertificateVerifyResult(18, isProxy: false);
        events.ReportTlsEarlyData(-7);
        events.ReportDataSent([4, 5]);

        CollectionAssert.AreEqual(
            new[] { "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust", "VerifyResult 18 False", "EarlyData -7", "DataSent 2" },
            inner.Calls);
    }

    /// <summary>A clock that counts in microseconds and stands still until a test moves it.</summary>
    private sealed class MicrosecondClock : TimeProvider
    {
        public long Microseconds { get; set; }

        public override long TimestampFrequency => 1_000_000;

        public override long GetTimestamp() => Microseconds;
    }

    /// <summary>Records each event it is given as one line naming it and its payload's length.</summary>
    private sealed class CallRecordingEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Calls.Add($"Info {text}");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("Opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("Reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("Handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add($"TlsData {bytes.Length} {sent}");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("TlsMessage");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("TlsTrust");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add($"VerifyResult {verifyResult} {isProxy}");

        public void ReportTlsEarlyData(long bytes) => Calls.Add($"EarlyData {bytes}");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"RequestHeader {bytes.Length}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ResponseHeader {bytes.Length}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"DataSent {bytes.Length}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"DataReceived {bytes.Length}");
    }
}
