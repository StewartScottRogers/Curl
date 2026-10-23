using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.IntegrationTests.Host;
using Curl.Conformance.Differential.Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlEasy;

/// <summary>
/// Replays the tester's Libcurl easy-handle cases (<c>LibcurlEasy/HeaderRound.json</c>, its
/// <c>h_round.json</c>;
/// <see cref="LibcurlHeaderCases"/>, its <c>h_in.json</c>; and every named case of its
/// <c>easytest.py</c>, <c>LibcurlEasyCases/EasyCases.json</c>) through Curl's Libcurl and the
/// reference libcurl 8.22.0 against the loopback server, and writes the <c>easy_report.txt</c>
/// report (ADR-0792). Inconclusive unless <c>CURL_DIFFERENTIAL_REFERENCE_LIBRARY</c> names the
/// reference shared library; the report goes to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a
/// file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlEasyDifferentialTests
{
    /// <summary>The environment variable naming the reference libcurl 8.22.0 shared library.</summary>
    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBRARY";

    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    /// <summary>The address the tester's cases name in their URLs; they are moved to a free port pair, as other runs may hold this one.</summary>
    private const string TesterAddress = "127.0.0.1:18080";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_EasyHandleCases_WritesEasyReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 shared library.");
            return;
        }

        IEnumerable<LibcurlEasyCase> cases =
        [
            new("h_round", LibcurlEasyRoundReader.ReadEmbedded("LibcurlEasy/HeaderRound.json")),
            .. LibcurlHeaderCases.All,
            .. LibcurlEasyNamedCaseReader.ReadEmbedded(),
        ];
        await using var server = StartOnFreePorts();
        cases = cases.Select(easyCase => OnPort(easyCase, server.Port));
        // Each control request on a fresh connection: a pooled one sits idle while a slow case runs and the server may close it first.
        using var control = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        control.DefaultRequestHeaders.ConnectionClose = true;
        using var reference = new ReferenceLibcurlEasyDriver(referencePath);
        await using var library = CurlGlobal.Init();
        var outcomes = new List<LibcurlEasyCaseOutcome>();
        foreach (var easyCase in cases)
        {
            TestContext.WriteLine(easyCase.Name);
            await ControlAsync(control, server, "/reset");
            var real = reference.Run(easyCase.Rounds);
            var realWire = await ControlAsync(control, server, "/log");
            await ControlAsync(control, server, "/reset");
            var ours = CurlLibcurlEasyDriver.Run(library, easyCase.Rounds);
            var ourWire = await ControlAsync(control, server, "/log");
            outcomes.Add(LibcurlEasyComparer.Compare(easyCase.Name, real, ours, realWire, ourWire));
        }

        var root = Directory.CreateTempSubdirectory("difftest_easy_").FullName;
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "easy_report.txt");
        await File.WriteAllTextAsync(reportPath, LibcurlEasyReportWriter.Write(outcomes), TestContext.CancellationToken);
        TestContext.WriteLine($"{LibcurlEasyReportWriter.Summary(outcomes)}; report: {reportPath}");
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

    private static LibcurlEasyCase OnPort(LibcurlEasyCase easyCase, int port) =>
        easyCase with
        {
            Rounds = [.. easyCase.Rounds.Select(round => round with
            {
                Options = [.. round.Options.Select(option => option with { Text = option.Text?.Replace(TesterAddress, $"127.0.0.1:{port}", StringComparison.Ordinal) })],
            })],
        };

    /// <summary>Asks the control port for <paramref name="path"/>, trying again when the connection is reset before the answer arrives (the server faults stop the run instead).</summary>
    private async Task<byte[]> ControlAsync(HttpClient control, DifferentialLoopbackServer server, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await control.GetByteArrayAsync(new Uri($"http://127.0.0.1:{server.ControlPort}{path}"), TestContext.CancellationToken);
            }
            catch (HttpRequestException exception) when (attempt < 3)
            {
                server.ThrowIfFaulted();
                TestContext.WriteLine($"control {path} attempt {attempt}: {exception.Message}");
            }
        }
    }
}
