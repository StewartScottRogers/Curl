using System.Text;
using Curl.Extension.OpenTelemetry.Otlp;

namespace Curl.Extension.OpenTelemetry.UnitTests.Otlp;

[TestClass]
public sealed class OtlpHttpExporterTests
{
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"resourceSpans\":[]}");

    [TestMethod]
    public async Task ExportAsync_AnHttpEndpoint_PostsTheJsonBodyToItsPath_OtlpSpecificationOtlpHttpRequest()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        var connector = new RecordedConnector(connection);

        var failure = await new OtlpHttpExporter(connector).ExportAsync("http://collector:4318/v1/traces", Body, CancellationToken.None);

        Assert.IsNull(failure);
        Assert.AreEqual(new ConnectTarget("collector", 4318, false), connector.Target);
        Assert.AreEqual(
            "POST /v1/traces HTTP/1.1\r\nHost: collector:4318\r\nContent-Type: application/json\r\nContent-Length: 20\r\nConnection: close\r\n\r\n{\"resourceSpans\":[]}",
            connection.Written);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExportAsync_AnHttpsEndpointOnItsDefaultPort_ConnectsWithTlsAndSendsTheBareHost()
    {
        var connection = new RecordedConnection("HTTP/1.1 202 Accepted\r\n\r\n");
        var connector = new RecordedConnector(connection);

        var failure = await new OtlpHttpExporter(connector).ExportAsync("https://[::1]/v1/traces", Body, CancellationToken.None);

        Assert.IsNull(failure);
        Assert.AreEqual(new ConnectTarget("::1", 443, true), connector.Target);
        StringAssert.Contains(connection.Written, "\r\nHost: [::1]\r\n");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 503 Service Unavailable", "the collector answered HTTP/1.1 503 Service Unavailable", DisplayName = "a status that is not 2xx")]
    [DataRow("HTTP/1.1 199 Early", "the collector answered HTTP/1.1 199 Early", DisplayName = "a status below 200")]
    [DataRow("SSH-2.0 OpenSSH", "the collector sent no HTTP response", DisplayName = "not HTTP")]
    [DataRow("HTTP/1.1 abc", "the collector sent no HTTP response", DisplayName = "no status code")]
    [DataRow("", "the collector sent no HTTP response", DisplayName = "nothing")]
    public async Task ExportAsync_ACollectorThatDoesNotAccept_GivesWhy_OtlpSpecificationOtlpHttpResponse(string statusLine, string expected)
    {
        var connection = new RecordedConnection(statusLine.Length == 0 ? string.Empty : statusLine + "\r\n\r\n");

        var failure = await new OtlpHttpExporter(new RecordedConnector(connection)).ExportAsync("http://collector/v1/traces", Body, CancellationToken.None);

        Assert.AreEqual(expected, failure);
    }

    [TestMethod]
    public async Task ExportAsync_AStatusLineThatNeverEnds_ReadsNoMoreThanItsLimit()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 " + new string('x', 10000));

        var failure = await new OtlpHttpExporter(new RecordedConnector(connection)).ExportAsync("http://collector/v1/traces", Body, CancellationToken.None);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public async Task ExportAsync_TheConnectionFails_GivesWhy()
    {
        var refused = await new OtlpHttpExporter(new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Connection refused")))
            .ExportAsync("http://collector/v1/traces", Body, CancellationToken.None);
        var reset = await new OtlpHttpExporter(new RecordedConnector(new RecordedConnection(null)))
            .ExportAsync("http://collector/v1/traces", Body, CancellationToken.None);

        Assert.AreEqual("could not connect to collector: Connection refused", refused);
        Assert.AreEqual("An existing connection was forcibly closed by the remote host.", reset);
    }

    [TestMethod]
    [DataRow("ftp://collector/v1/traces")]
    [DataRow("not a url")]
    public async Task ExportAsync_AnEndpointThatIsNotHttp_GivesWhyWithoutConnecting(string endpoint)
    {
        var connector = new RecordedConnector(new RecordedConnection(string.Empty));

        var failure = await new OtlpHttpExporter(connector).ExportAsync(endpoint, Body, CancellationToken.None);

        Assert.AreEqual($"the endpoint \"{endpoint}\" is not an http or https URL", failure);
        Assert.IsNull(connector.Target);
    }
}
