using Curl.Conformance.Differential.IntegrationTests.CliProtocols.Host;
using Curl.Conformance.Differential.IntegrationTests.CliTls;
using Curl.Conformance.Differential.IntegrationTests.Host;
using Curl.Conformance.Differential.Protocols.Binary;
using Curl.Conformance.Differential.Protocols.Core;
using Curl.Conformance.Differential.Protocols.Ftp;
using Curl.Conformance.Differential.Protocols.Mail;
using Curl.Conformance.Differential.Protocols.Proxy;
using Curl.Conformance.Differential.Protocols.Text;
using Curl.Conformance.Differential.Tls;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols;

/// <summary>
/// Replays the tester's CLI protocol and proxy cases (every case file under <c>CliProtocols</c>, ported from
/// <c>cases_proto.py</c>, <c>cases_proto2.py</c> and <c>cases_proxy.py</c>) through Curl and the reference
/// curl against the C# ports of proto_servers.py, proto2_servers.py and proxy_server.py, the plain loopback
/// server for <c>{U}</c> and the TLS loopback servers for <c>https://127.0.0.1:18100</c>, and writes the
/// <c>### DIFF</c> report (ADR-0782). Every protocol server shares one wire log, read from the FTP server's
/// control port, so the cases run one at a time. Inconclusive unless <c>CURL_DIFFERENTIAL_REFERENCE</c> names
/// the reference binary and <c>CURL_DIFFERENTIAL_PKI</c> names the tester's <c>pki</c> directory; the report
/// goes to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CliProtocolsDifferentialTests
{
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";
    private const string PkiVariable = "CURL_DIFFERENTIAL_PKI";
    private const string CaseFolder = "CliProtocols";
    private const string EmbeddedCasePrefix = "Curl.Conformance.Differential.Cases.";

    private const int LoopbackPort = 18080;
    private const int FtpPort = 18200;
    private const int SmtpPort = 18210;
    private const int Pop3Port = 18220;
    private const int ImapPort = 18230;
    private const int ProxyPort = 18300;
    private const int DictPort = 18400;
    private const int GopherPort = 18410;
    private const int TelnetPort = 18420;
    private const int RtspPort = 18430;
    private const int WebSocketPort = 18440;
    private const int MqttPort = 18450;
    private const int TftpPort = 18460;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_CliProtocolCaseFiles_WritesDiffReport()
    {
        var reference = DifferentialBinaries.FindReference();
        var pki = Environment.GetEnvironmentVariable(PkiVariable);
        if (reference is null || string.IsNullOrEmpty(pki))
        {
            Assert.Inconclusive($"{DifferentialBinaries.ReferenceVariable} and {PkiVariable} must name the reference curl 8.22.0 binary and the tester's pki directory.");
            return;
        }

        var cases = ReadCliProtocolCases().Select(differentialCase => DifferentialTlsEndpoints.Substitute(differentialCase, pki)).ToList();
        var root = Directory.CreateTempSubdirectory("difftest_").FullName;
        var log = new ProtocolWireLog();
        var connector = new TcpProxyTunnelConnector();
        await using var server = DifferentialLoopbackServer.Start(LoopbackPort);
        await using var tlsServer = DifferentialTlsLoopbackServer.Start(pki);
        await using var ftp = ProtocolLoopbackHost.Start(FtpPort, log, new FtpResponder(log, new LoopbackPassiveDataListenerOpener()).RespondAsync);
        await using var smtp = ProtocolLoopbackHost.Start(SmtpPort, log, new SmtpResponder(log).RespondAsync);
        await using var pop3 = ProtocolLoopbackHost.Start(Pop3Port, log, new Pop3Responder(log).RespondAsync);
        await using var imap = ProtocolLoopbackHost.Start(ImapPort, log, new ImapResponder(log).RespondAsync);
        await using var proxy = ProtocolLoopbackHost.Start(
            ProxyPort, log, new ProxyServerResponder(log, connector, new HttpProxyResponder(log, connector, requiresAuthentication: false)).RespondAsync);
        await using var dict = ProtocolLoopbackHost.Start(DictPort, log, new DictResponder(log).RespondAsync);
        await using var gopher = ProtocolLoopbackHost.Start(GopherPort, log, new GopherResponder(log).RespondAsync);
        await using var telnet = ProtocolLoopbackHost.Start(TelnetPort, log, new TelnetResponder(log, TimeProvider.System).RespondAsync);
        await using var rtsp = ProtocolLoopbackHost.Start(RtspPort, log, new RtspResponder(log).RespondAsync);
        await using var webSocket = ProtocolLoopbackHost.Start(WebSocketPort, log, new WebSocketResponder(log).RespondAsync);
        await using var mqtt = ProtocolLoopbackHost.Start(MqttPort, log, new MqttResponder(log).RespondAsync);
        await using var tftp = TftpLoopbackHost.Start(TftpPort, log);
        var runner = new DifferentialCaseRunner(
            new DifferentialProcessRunner(root, server.BaseUrl), ftp.ControlPort, DifferentialBinaries.FindOurs(), [reference]);

        var outcomes = await runner.RunAsync(cases);

        var report = DifferentialReportWriter.Write(outcomes);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(reportPath, report, TestContext.CancellationToken);
        TestContext.WriteLine($"Report: {reportPath}");
        Assert.HasCount(cases.Count(differentialCase => differentialCase.RunsAgainstReferenceBuild), outcomes);
    }

    /// <summary>Reads every case file embedded under <c>Cases/CliProtocols</c>, in resource name order.</summary>
    private static List<DifferentialCase> ReadCliProtocolCases() =>
        [.. typeof(DifferentialCaseFileReader).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(EmbeddedCasePrefix + CaseFolder + ".", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .SelectMany(name => DifferentialCaseFileReader.ReadEmbedded(name[EmbeddedCasePrefix.Length..]))];
}
