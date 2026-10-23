using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.IntegrationTests.Host;
using Curl.Conformance.Differential.Libcurl.Easy;
using Curl.Conformance.Differential.Libcurl.Multi;
using Libcurl.Global;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlMulti;

/// <summary>
/// Replays the tester's Libcurl multi cases (<c>LibcurlMulti/m_in.json</c>, <c>m2.json</c>,
/// <c>m2i.json</c>, <c>m3.json</c>) and share case (<c>LibcurlMulti/sh.json</c>) through Curl's
/// Libcurl and the reference libcurl 8.22.0 against the loopback server, and writes the
/// <c>multi_report.txt</c> report (ADR-0793). Inconclusive unless
/// <c>CURL_DIFFERENTIAL_REFERENCE_LIBRARY</c> names the reference shared library; the report goes
/// to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlMultiDifferentialTests
{
    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBRARY";
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";
    private const string TesterAddress = "127.0.0.1:18080";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_MultiAndShareCases_WritesMultiReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 shared library.");
            return;
        }

        await using var server = StartOnFreePorts();
        var address = $"127.0.0.1:{server.Port}";
        using var reference = new ReferenceLibcurlMultiLibrary(referencePath);
        var outcomes = new List<LibcurlEasyCaseOutcome>();
        foreach (var name in new[] { "m_in", "m2", "m2i", "m3" })
        {
            var cases = LibcurlMultiCaseReader.ReadMulti(LibcurlMultiCaseReader.ReadEmbedded($"LibcurlMulti/{name}.json").Replace(TesterAddress, address, StringComparison.Ordinal));
            var real = new List<string>();
            var ours = new List<string>();
            for (var index = 0; index < cases.Count; index++)
            {
                await using (var target = new ReferenceLibcurlMultiTarget(reference))
                {
                    real.Add(await new LibcurlMultiReplayer(target, TimeProvider.System).ReplayMultiAsync(index, cases[index]));
                }

                await using var library = CurlGlobal.Init();
                await using var ourTarget = new CurlLibcurlMultiTarget(library);
                ours.Add(await new LibcurlMultiReplayer(ourTarget, TimeProvider.System).ReplayMultiAsync(index, cases[index]));
            }

            outcomes.Add(LibcurlMultiComparer.Compare(name, real, ours));
        }

        var share = LibcurlMultiCaseReader.ReadShare(LibcurlMultiCaseReader.ReadEmbedded("LibcurlMulti/sh.json").Replace(TesterAddress, address, StringComparison.Ordinal));
        string realShare;
        await using (var target = new ReferenceLibcurlMultiTarget(reference))
        {
            realShare = await new LibcurlMultiReplayer(target, TimeProvider.System).ReplayShareAsync(share);
        }

        await using (var library = CurlGlobal.Init())
        await using (var ourTarget = new CurlLibcurlMultiTarget(library))
        {
            outcomes.Add(LibcurlMultiComparer.Compare("sh", [realShare], [await new LibcurlMultiReplayer(ourTarget, TimeProvider.System).ReplayShareAsync(share)]));
        }

        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named
            ? named
            : Path.Combine(Directory.CreateTempSubdirectory("difftest_multi_").FullName, "multi_report.txt");
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
}
