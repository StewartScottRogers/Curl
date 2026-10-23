using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.IntegrationTests.Host;
using Curl.Conformance.Differential.Libcurl.Mime;
using Libcurl.Global;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMime;

/// <summary>
/// Replays the tester's Libcurl MIME cases (<see cref="LibcurlMimeCases"/>, its
/// <c>mimetest.py</c>) through Curl's Libcurl and the reference libcurl 8.22.0 against the
/// loopback server's <c>/echo</c>, and writes the report <c>mimetest.py</c> printed (ADR-0792).
/// Inconclusive unless <c>CURL_DIFFERENTIAL_REFERENCE_LIBRARY</c> names the reference shared
/// library; the report goes to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file, else to a
/// temporary directory, which also holds the <c>mimefile.txt</c> and <c>mimefile.json</c> the
/// cases post.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlMimeDifferentialTests
{
    /// <summary>The environment variable naming the reference libcurl 8.22.0 shared library.</summary>
    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBRARY";

    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_MimeCases_WritesMimeReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 shared library.");
            return;
        }

        var root = Directory.CreateTempSubdirectory("difftest_mime_").FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "mimefile.txt"), LibcurlMimeCases.TextFileContent, TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "mimefile.json"), LibcurlMimeCases.JsonFileContent, TestContext.CancellationToken);
        await using var server = StartOnFreePorts();
        var url = $"http://127.0.0.1:{server.Port}/echo";
        using var control = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        using var reference = new ReferenceLibcurlMimeDriver(referencePath);
        await using var library = CurlGlobal.Init();
        var outcomes = new List<LibcurlMimeCaseOutcome>();
        foreach (var mimeCase in LibcurlMimeCases.All)
        {
            await ControlAsync(control, server, "/reset");
            var real = reference.Run(mimeCase, url, root);
            var realWire = await ControlAsync(control, server, "/log");
            await ControlAsync(control, server, "/reset");
            var ours = CurlLibcurlMimeDriver.Run(library, mimeCase, url, root);
            var ourWire = await ControlAsync(control, server, "/log");
            outcomes.Add(LibcurlMimeComparer.Compare(mimeCase.Name, real, ours, realWire, ourWire));
        }

        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "mime_report.txt");
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
