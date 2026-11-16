using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins BL-5286 (ADR-1281) through the composed handlers: <c>smb://</c> and <c>smbs://</c> reach the SMB client,
/// which connects, instead of failing as disabled.
/// </summary>
[TestClass]
public sealed class SmbEnabledTests
{
    private const string ConnectFailure = "Failed to connect to 127.0.0.1 port 20002";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("smb://127.0.0.1:20002/share/x")]
    [DataRow("smbs://127.0.0.1:20002/share/x")]
    public async Task DispatchAsync_SmbUrl_ConnectsInsteadOfFailingAsDisabled(string url)
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

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.HasCount(1, connector.Targets);
        Assert.AreEqual(0, output.Length);
    }
}
