using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Http;

/// <summary>Pins that the request and response heads reach <c>-v</c> and the <c>-i</c> or <c>-D</c> stream.</summary>
[TestClass]
public sealed class OciHttpHeadReporterTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task Report_AfterAResponseHead_GivesStatusVersionContentTypeAndSize()
    {
        var reporter = new OciHttpHeadReporter(NoTransferEvents.Instance, null);
        Assert.IsNull(reporter.Report);

        reporter.ReportRequestHead("GET / HTTP/1.1\r\n\r\n"u8);
        foreach (string line in new[] { "HTTP/1.1 200 OK", "content-type:  application/octet-stream ", "X: y", "" })
        {
            await reporter.ReportResponseLineAsync(line, CancellationToken.None);
        }

        TransferReport report = reporter.Report!;
        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(new Version(1, 1), report.HttpVersion);
        Assert.AreEqual("application/octet-stream", report.ContentType);
        Assert.AreEqual(17 + 42 + 6 + 2, report.HeaderSize);
    }

    [TestMethod]
    public async Task Report_AnInterimHeadThenTheFinalHead_GivesTheFinalStatusAndContentType()
    {
        var reporter = new OciHttpHeadReporter(NoTransferEvents.Instance, null);

        foreach (string line in new[] { "HTTP/1.1 100 Continue", "", "HTTP/1.1 200 OK", "Content-Type: text/plain", "" })
        {
            await reporter.ReportResponseLineAsync(line, CancellationToken.None);
        }

        TransferReport report = reporter.Report!;
        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual("text/plain", report.ContentType);
        Assert.AreEqual(23 + 2 + 17 + 26 + 2, report.HeaderSize);
    }

    [TestMethod]
    public async Task Report_AnHttp10ResponseWithABadStatus_GivesVersion10AndCodeZero()
    {
        var reporter = new OciHttpHeadReporter(NoTransferEvents.Instance, null);

        reporter.ReportRequestHead("GET / HTTP/1.1\r\n\r\n"u8);
        await reporter.ReportResponseLineAsync("HTTP/1.0", CancellationToken.None);

        Assert.AreEqual(new Version(1, 0), reporter.Report!.HttpVersion);
        Assert.AreEqual(0, reporter.Report.ResponseCode);
        Assert.IsNull(reporter.Report.ContentType);
    }

    [TestMethod]
    public async Task ReportWith_AfterAResponseHead_CarriesTheDownloadSize()
    {
        var reporter = new OciHttpHeadReporter(NoTransferEvents.Instance, null);

        Assert.IsNull(reporter.ReportWith(5));
        reporter.ReportRequestHead("GET / HTTP/1.1\r\n\r\n"u8);
        await reporter.ReportResponseLineAsync("HTTP/1.1 200 OK", CancellationToken.None);

        Assert.AreEqual(5, reporter.ReportWith(5)!.DownloadSize);
        Assert.AreEqual(0, reporter.Report!.DownloadSize);
    }

    [TestMethod]
    public async Task ReportResponseLineAsync_WithHeaderOutput_WritesTheLineWithCrlfToBothPlaces()
    {
        var events = new HeadEvents();
        var headerOutput = new MemoryStream();

        await new OciHttpHeadReporter(events, headerOutput).ReportResponseLineAsync("HTTP/1.1 200 OK", CancellationToken.None);

        Assert.AreEqual("HTTP/1.1 200 OK\r\n", events.Response.ToString());
        Assert.AreEqual("HTTP/1.1 200 OK\r\n", Encoding.Latin1.GetString(headerOutput.ToArray()));
    }

    [TestMethod]
    public async Task ReportResponseLineAsync_WithoutHeaderOutput_ReportsOnlyToEvents()
    {
        var events = new HeadEvents();

        await new OciHttpHeadReporter(events, null).ReportResponseLineAsync("X: y", CancellationToken.None);

        Assert.AreEqual("X: y\r\n", events.Response.ToString());
    }

    [TestMethod]
    public void ReportRequestHead_Always_ReportsTheBytes()
    {
        var events = new HeadEvents();

        new OciHttpHeadReporter(events, null).ReportRequestHead("GET / HTTP/1.1\r\n\r\n"u8);

        Assert.AreEqual("GET / HTTP/1.1\r\n\r\n", events.Request.ToString());
    }

    [TestMethod]
    public async Task ReportResponseLineAsync_None_ShowsNothing()
    {
        OciHttpHeadReporter.None.ReportRequestHead("x"u8);
        await OciHttpHeadReporter.None.ReportResponseLineAsync("x", CancellationToken.None);

        Assert.IsNotNull(OciHttpHeadReporter.None);
    }

    [TestMethod]
    public async Task Exchange_WithReporter_ShowsTheWholeRequestHeadAndEveryResponseLine()
    {
        var events = new HeadEvents();
        var headerOutput = new MemoryStream();
        var connection = new RecordedConnection(Response);
        var reporter = new OciHttpHeadReporter(events, headerOutput);
        await new OciHttpClient(new RecordedConnector(connection), reporter).SendAsync(
            new OciHttpRequest("GET", new Uri("https://registry.example.com/v2/a/manifests/latest"), [], null), CancellationToken.None);

        StringAssert.StartsWith(events.Request.ToString(), "GET /");
        Assert.AreEqual(Response, events.Response.ToString());
        Assert.AreEqual(Response, Encoding.Latin1.GetString(headerOutput.ToArray()));
    }

    private sealed class HeadEvents : ITransferEvents
    {
        public StringBuilder Request { get; } = new();

        public StringBuilder Response { get; } = new();

        public void ReportInfo(string text)
        {
        }

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Request.Append(Encoding.Latin1.GetString(bytes));

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Response.Append(Encoding.Latin1.GetString(bytes));

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
