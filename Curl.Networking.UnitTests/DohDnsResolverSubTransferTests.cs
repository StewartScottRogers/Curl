using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the lines each DoH sub-transfer of <see cref="DohDnsResolver" /> reports on its
/// <see cref="DohDnsResolver.SubTransferEvents" /> (BL-1180): the queries run one after another, A's
/// lines whole before AAAA's (ADR-0380), each with curl 8.21.0's <c>using HTTP/1.x</c>, POST head and
/// body, <c>upload completely sent off</c>, answer head and body, <c>left intact</c> and
/// for a query that succeeded <c>a DoH request is completed</c> lines (BL-2970), before any <c>[DNS] </c> prefix the console adds.
/// </summary>
[TestClass]
public sealed class DohDnsResolverSubTransferTests
{
    private static readonly Uri DohUrl = new("http://127.0.0.1:47112/dns-query");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ResolveAsync_WithSubTransferEvents_ReportsEachSubTransferWholeInTurn()
    {
        // Neither answer decodes, so curl 8.22.0 ends each with its error line, not a completed line (BL-2970).
        var events = new SubTransferRecorder();
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc"));
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"));

        Diagnostics.Arrange("DoH URL", DohUrl);
        Diagnostics.Arrange("answers queued", connector.BytesToRead.Count);
        Diagnostics.Arrange("connect failure", connector.Failure?.ErrorMessage ?? "(none)");

        await new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events }.ResolveAsync("example.test", CancellationToken.None);

        WriteEventLinesAndLastLine(events, "* Connection #2 to host 127.0.0.1:47112 left intact");

        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "> POST /dns-query HTTP/1.1\r\nHost: 127.0.0.1:47112\r\nAccept: */*\r\nContent-Type: application/dns-message\r\nContent-Length: 30\r\n\r\n",
                "} 30",
                "* upload completely sent off: 30 bytes",
                "< HTTP/1.1 200 OK\r\n",
                "< Content-Length: 3\r\n",
                "< \r\n",
                "{ 3",
                "* Connection #1 to host 127.0.0.1:47112 left intact",
                "* using HTTP/1.x",
                "> POST /dns-query HTTP/1.1\r\nHost: 127.0.0.1:47112\r\nAccept: */*\r\nContent-Type: application/dns-message\r\nContent-Length: 30\r\n\r\n",
                "} 30",
                "* upload completely sent off: 30 bytes",
                "< HTTP/1.1 200 OK\r\n",
                "< Content-Length: 0\r\n",
                "< \r\n",
                "* Connection #2 to host 127.0.0.1:47112 left intact",
            },
            events.Lines);
        var everyTargetReports = connector.Targets.All(target => ReferenceEquals(events, target.Events));
        Diagnostics.Assert("every target reports to the sub-transfer events", true, everyTargetReports);
        Assert.IsTrue(everyTargetReports);
    }

    [TestMethod]
    public async Task ResolveAsync_WithSubTransferEventsAndAFailedConnect_ReportsNoSubTransferLine()
    {
        var events = new SubTransferRecorder();
        var connector = new FakeConnector { Failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect") };

        Diagnostics.Arrange("DoH URL", DohUrl);
        Diagnostics.Arrange("answers queued", connector.BytesToRead.Count);
        Diagnostics.Arrange("connect failure", connector.Failure?.ErrorMessage ?? "(none)");

        await new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events }.ResolveAsync("example.test", CancellationToken.None);

        WriteEventLinesAndLastLine(events, null);

        // curl 8.22.0 ends a failed query with its error line on the DoH trace, not a completed line (BL-2970).
        Assert.IsEmpty(events.Lines);
    }

    [TestMethod]
    public async Task ResolveAsync_WithSubTransferEventsAndACutShortAnswer_ReportsNoConnectionLeftIntact()
    {
        var events = new SubTransferRecorder();
        var connector = new FakeConnector();
        connector.BytesToRead.Add(Response("HTTP/1.1 200 OK\r\n"));
        Diagnostics.Arrange("DoH URL", DohUrl);
        Diagnostics.Arrange("address family", System.Net.Sockets.AddressFamily.InterNetwork);
        Diagnostics.Arrange("answer", "a status line, then the close");

        await new DohDnsResolver(connector, DohUrl) { SubTransferEvents = events, AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork }
            .ResolveAsync("example.test", CancellationToken.None);

        WriteEventLinesAndLastLine(events, "< HTTP/1.1 200 OK\r\n");

        // curl 8.22.0 ends a failed query with its error line on the DoH trace in place of the
        // completed line (BL-2970).
        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                "> POST /dns-query HTTP/1.1\r\nHost: 127.0.0.1:47112\r\nAccept: */*\r\nContent-Type: application/dns-message\r\nContent-Length: 30\r\n\r\n",
                "} 30",
                "* upload completely sent off: 30 bytes",
                "< HTTP/1.1 200 OK\r\n",
            },
            events.Lines);
    }

    private static byte[] Response(string text) => Encoding.Latin1.GetBytes(text);

    private void WriteEventLinesAndLastLine(SubTransferRecorder events, string? expectedLastLine)
    {
        Diagnostics.Act("event lines", events.Lines.Count);
        foreach (var line in events.Lines)
        {
            Diagnostics.Act("event line", line.Replace("\r\n", "\\r\\n", StringComparison.Ordinal));
        }

        Diagnostics.Assert("last event line", expectedLastLine, events.Lines.LastOrDefault());
    }

    /// <summary>Records each event as one line: <c>* </c> info, <c>&gt; </c> and <c>&lt; </c> heads, <c>}</c> and <c>{</c> byte counts.</summary>
    private sealed class SubTransferRecorder : ITransferEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string text) => Lines.Add("* " + text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Lines.Add("opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Lines.Add("reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Lines.Add("handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Lines.Add("tls");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Lines.Add("> " + Encoding.Latin1.GetString(bytes));

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Lines.Add("< " + Encoding.Latin1.GetString(bytes));

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Lines.Add($"}} {bytes.Length}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Lines.Add($"{{ {bytes.Length}");
    }
}
