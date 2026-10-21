using Curl.Console;
using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // GF-0002 (BL-1998): a tool outside the test projects, such as the gap office's, reaches Curl only
    // through the public InProcessCurl, over the in-memory connector or the production TcpConnector with
    // only the dial replaced, so the connection-reuse cases must pass both ways too.
    [TestMethod]
    [DataRow(48, false)]
    [DataRow(48, true)]
    [DataRow(338, false)]
    [DataRow(338, true)]
    [DataRow(435, false)]
    [DataRow(435, true)]
    [DataRow(471, false)]
    [DataRow(471, true)]
    [DataRow(1074, false)]
    [DataRow(1074, true)]
    [DataRow(1134, false)]
    [DataRow(1134, true)]
    [DataRow(1418, false)]
    [DataRow(1418, true)]
    [DataRow(1419, false)]
    [DataRow(1419, true)]
    [DataRow(1421, false)]
    [DataRow(1421, true)]
    [DataRow(1479, false)]
    [DataRow(1479, true)]
    public async Task InProcessCurl_ConnectionReuseCase_Passes(int testNumber, bool dialsThroughTheTcpConnector)
    {
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test{testNumber}-{Guid.NewGuid():N}"));
        try
        {
            UpstreamCaseRunner runner = new(
                invocation => dialsThroughTheTcpConnector
                    ? InProcessCurl.RunAsync(invocation.Arguments, invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, new InMemoryServerTcpDialer(invocation.Connector), new LoopbackOnlyDnsResolver(), invocation.DatagramConnector)
                    : InProcessCurl.RunAsync(invocation.Arguments, invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, invocation.Connector, invocation.DatagramConnector),
                Platform,
                TimeProvider.System,
                TimeLimit,
                SshServer,
                Http2Server);
            UpstreamCaseOutcome outcome = await runner.RunAsync(testNumber, testFile, logDirectory.FullName, certificateDirectory: CertificateFolder.Value);

            // test471 needs http/2, which the Schannel build Curl matches on Windows lacks (ADR-0488, BL-2230).
            if (outcome.RunsOnlyOnAnotherPlatform)
            {
                Assert.Inconclusive(outcome.Detail);
            }

            Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        }
        finally
        {
            DeleteOrKeepLogDirectory(logDirectory);
        }
    }
}
