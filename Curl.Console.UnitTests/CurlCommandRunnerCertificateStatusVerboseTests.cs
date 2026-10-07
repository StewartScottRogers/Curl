using System.Text;

using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-v</c>'s <c>--cert-status</c> line as the console writes it (BL-875): the
/// hand-built TLS provider reports <c>SSL certificate status: ...</c> once it has read the
/// stapled response, and curl 8.18.0 on OpenSSL 3.5.5 printed it, measured in BL-610, as
/// <c>* SSL certificate status: good (0)</c>, or <c>revoked (1)</c> followed by the exit 91
/// reason.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerCertificateStatusVerboseTests
{
    private readonly InMemoryFileSystem fileSystem = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_WithAGoodStapledResponse_WritesTheGoodStatusLine()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length 1, body a");
        var connector = new StatusReportingConnector(
            "SSL certificate status: good (0)",
            new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\na")]));

        int exitCode = await RunAsync(connector);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "stderr has the good status line",
            true,
            StandardError.Contains("* SSL certificate status: good (0)\n", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardError, "* SSL certificate status: good (0)\n");
    }

    [TestMethod]
    public async Task RunAsync_WithARevokedStapledResponse_WritesTheRevokedStatusLineBeforeExit91()
    {
        Diagnostics.Arrange("connector failure", "exit 91, SSL certificate revocation reason: keyCompromise (1)");
        var connector = new StatusReportingConnector(
            "SSL certificate status: revoked (1)",
            new RecordingConnector(CurlExitCode.SslInvalidCertStatus, "SSL certificate revocation reason: keyCompromise (1)"));

        int exitCode = await RunAsync(connector);

        Diagnostics.Assert("exit code", 91, exitCode);
        var standardError = StandardError;
        var statusLine = standardError.IndexOf("* SSL certificate status: revoked (1)\n", StringComparison.Ordinal);
        var reasonLine = standardError.IndexOf("curl: (91) SSL certificate revocation reason: keyCompromise (1)", StringComparison.Ordinal);
        Diagnostics.Assert("status line found", true, statusLine >= 0);
        Diagnostics.Assert("reason line comes after the status line", true, reasonLine > statusLine);
        Assert.AreEqual(91, exitCode);
        Assert.IsGreaterThanOrEqualTo(0, statusLine, standardError);
        Assert.IsGreaterThan(statusLine, standardError.IndexOf("curl: (91) SSL certificate revocation reason: keyCompromise (1)", StringComparison.Ordinal), standardError);
    }

    private string StandardError => Encoding.UTF8.GetString(standardError.ToArray());

    private async Task<int> RunAsync(IConnector connector)
    {
        string[] arguments = ["-v", "-o", "out", "--cert-status", "http://h:18234/"];
        Diagnostics.Arrange("command line", string.Join(" ", arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(
                        new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())),
                        []),
                    fileSystem,
                    fileSystem,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    /// <summary>Reports the status line on the target's events, as the TLS provider does, then connects through another connector.</summary>
    private sealed class StatusReportingConnector(string statusLine, IConnector inner) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            target.Events.ReportInfo(statusLine);
            return inner.ConnectAsync(target, cancellationToken);
        }
    }
}
