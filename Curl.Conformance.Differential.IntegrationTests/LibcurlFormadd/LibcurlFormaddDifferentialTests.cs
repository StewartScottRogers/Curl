using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.IntegrationTests.Host;
using Curl.Conformance.Differential.Libcurl.Formadd;
using Curl.Conformance.Differential.Libcurl.Mime;
using Libcurl.Global;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlFormadd;

/// <summary>
/// Replays the formadd cases (<see cref="LibcurlFormaddCases"/>) through Curl's Libcurl and the
/// reference libcurl 8.22.0: each case's <c>curl_formadd</c> codes, its <c>curl_formget</c> output
/// and its <c>CURLOPT_HTTPPOST</c> post to the loopback server's <c>/echo</c>, compared and
/// reported as the MIME cases are (ADR-0792). Inconclusive unless
/// <c>CURL_DIFFERENTIAL_REFERENCE_LIBRARY</c> names the reference shared library; the report goes
/// to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file, else to a temporary directory, which
/// also holds the <c>formfile.txt</c> and <c>formfile.json</c> the cases post.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlFormaddDifferentialTests
{
    /// <summary>The environment variable naming the reference libcurl 8.22.0 shared library.</summary>
    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBRARY";

    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_FormaddCases_WritesFormaddReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 shared library.");
            return;
        }

        var root = Directory.CreateTempSubdirectory("difftest_formadd_").FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "formfile.txt"), LibcurlFormaddCases.TextFileContent, TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "formfile.json"), LibcurlFormaddCases.JsonFileContent, TestContext.CancellationToken);
        await using var server = StartOnFreePorts();
        var url = $"http://127.0.0.1:{server.Port}/echo";
        using var control = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        using var reference = new ReferenceLibcurlFormaddDriver(referencePath);
        await using var library = CurlGlobal.Init();
        var outcomes = new List<LibcurlMimeCaseOutcome>();
        foreach (var formCase in LibcurlFormaddCases.All)
        {
            await ControlAsync(control, server, "/reset");
            var real = reference.Run(formCase, url, root);
            var realWire = await ControlAsync(control, server, "/log");
            await ControlAsync(control, server, "/reset");
            var ours = CurlLibcurlFormaddDriver.Run(library, formCase, url, root);
            var ourWire = await ControlAsync(control, server, "/log");
            outcomes.Add(LibcurlMimeComparer.Compare(
                formCase.Name, real.Log, ours.Log, LibcurlFormaddLogWriter.Join(real.FormGet, realWire), LibcurlFormaddLogWriter.Join(ours.FormGet, ourWire)));
        }

        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "formadd_report.txt");
        await File.WriteAllTextAsync(reportPath, LibcurlMimeReportWriter.Summary(outcomes) + "\n" + LibcurlMimeReportWriter.Write(outcomes), TestContext.CancellationToken);
        TestContext.WriteLine($"{LibcurlMimeReportWriter.Summary(outcomes)}; report: {reportPath}");
        Assert.IsNotEmpty(outcomes);
    }

    private static DifferentialLoopbackServer StartOnFreePorts()
    {
        for (var attempt = 1; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            try
            {
                return DifferentialLoopbackServer.Start(port);
            }
            catch (SocketException) when (attempt < 10)
            {
            }
        }
    }

    private async Task<byte[]> ControlAsync(HttpClient control, DifferentialLoopbackServer server, string path) =>
        await control.GetByteArrayAsync(new Uri($"http://127.0.0.1:{server.ControlPort}{path}"), TestContext.CancellationToken);
}
