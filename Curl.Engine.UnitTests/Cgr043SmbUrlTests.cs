using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-043 part 1 (BL-3887) through the composed handlers: curl 8.22.0 (Schannel, Windows), measured on
/// 2026-10-26, answers <c>curl -sS smb://127.0.0.1:20002/share/x</c> with exit 1 and
/// <c>curl: (1) Protocol "smb" is disabled</c>, <c>smbs://</c> the same, and connects nowhere (ADR-0835).
/// </summary>
[TestClass]
public sealed class Cgr043SmbUrlTests
{
    private const string ConnectFailure = "Failed to connect to 127.0.0.1 port 20002";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("smb://127.0.0.1:20002/share/x", "Protocol \"smb\" is disabled")]
    [DataRow("smbs://127.0.0.1:20002/share/x", "Protocol \"smbs\" is disabled")]
    public async Task DispatchAsync_SmbUrl_FailsWithExitOneAsDisabledWithoutConnecting(string url, string expected)
    {
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, ConnectFailure);
        ProtocolDispatcher dispatcher = new(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver()));
        using MemoryStream output = new();
        Diagnostics.Arrange("url", url);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = output });
        Diagnostics.Act("result", $"{result.ExitCode} {result.ErrorMessage}");
        Diagnostics.Act("connect targets", connector.Targets.Count);

        Diagnostics.Assert("exit code and message", $"{CurlExitCode.UnsupportedProtocol} {expected}", $"{result.ExitCode} {result.ErrorMessage}");
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
        Assert.IsEmpty(connector.Targets);
        Assert.AreEqual(0, output.Length);
    }
}
