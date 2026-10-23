using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Conformance.Differential.Libcurl.Easy;
using Curl.Conformance.Differential.Libcurl.WebSocket;
using Curl.Conformance.Differential.Protocols.Core;
using Libcurl.Global;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlWebSocket;

/// <summary>
/// Replays <see cref="LibcurlWebSocketCases.All"/> through Curl's Libcurl and the reference
/// libcurl 8.22.0 against a loopback <see cref="LibcurlWebSocketResponder"/>, one connection per
/// case and library, and writes the <c>ws_report.txt</c> report in the <c>easy_report.txt</c>
/// shape (BL-3240). Inconclusive unless <c>CURL_DIFFERENTIAL_REFERENCE_LIBRARY</c> names the
/// reference shared library; the report goes to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a
/// file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlWebSocketDifferentialTests
{
    /// <summary>How many more times a receive answering <c>CURLE_AGAIN</c> is called before that answer is recorded.</summary>
    internal const int ReceiveAttempts = 200;

    /// <summary>The pause between two receives that answered <c>CURLE_AGAIN</c>.</summary>
    internal static readonly TimeSpan ReceivePause = TimeSpan.FromMilliseconds(10);

    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBRARY";

    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    private static readonly TimeSpan ServerLimit = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(600_000, CooperativeCancellation = true)]
    public async Task RunAsync_WebSocketCases_WritesWebSocketReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 shared library.");
            return;
        }

        var cancellationToken = TestContext.CancellationToken;
        using var reference = new ReferenceLibcurlWebSocketDriver(referencePath);
        await using var library = CurlGlobal.Init();
        var outcomes = new List<LibcurlEasyCaseOutcome>();
        foreach (var webSocketCase in LibcurlWebSocketCases.All)
        {
            var (real, realWire) = await ServeAsync(webSocketCase, url => reference.RunAsync(webSocketCase, url, cancellationToken), cancellationToken);
            var (ours, ourWire) = await ServeAsync(webSocketCase, url => CurlLibcurlWebSocketDriver.RunAsync(library, webSocketCase, url, cancellationToken), cancellationToken);
            outcomes.Add(LibcurlEasyComparer.Compare(webSocketCase.Name, real, ours, realWire, ourWire));
        }

        var root = Directory.CreateTempSubdirectory("difftest_ws_").FullName;
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "ws_report.txt");
        await File.WriteAllTextAsync(reportPath, LibcurlEasyReportWriter.Write(outcomes), cancellationToken);
        TestContext.WriteLine($"{LibcurlEasyReportWriter.Summary(outcomes)}; report: {reportPath}");
        Assert.HasCount(LibcurlWebSocketCases.All.Count, outcomes);
    }

    private static async Task<(IReadOnlyList<string> Lines, byte[] Wire)> ServeAsync(
        LibcurlWebSocketCase webSocketCase, Func<string, Task<IReadOnlyList<string>>> run, CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var log = new ProtocolWireLog();
        using var serving = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var server = ServeOneAsync(listener, new LibcurlWebSocketResponder(webSocketCase, log), serving.Token);
            var address = $"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
            var lines = await Task.Run(() => run($"ws://{address}/ws"), cancellationToken);
            serving.CancelAfter(ServerLimit);
            await server;

            // Each run listens on its own port, so the Host line's port is blanked.
            return (lines, Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(log.Take()).Replace(address, "127.0.0.1:PORT", StringComparison.Ordinal)));
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task ServeOneAsync(TcpListener listener, LibcurlWebSocketResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await responder.RespondAsync(client.GetStream(), cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException)
        {
        }
    }
}
