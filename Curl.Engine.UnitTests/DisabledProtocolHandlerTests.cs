using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins the refusal curl 8.22.0 (Schannel, Windows) gives an <c>smb://</c> or <c>smbs://</c> URL, measured
/// on 2026-10-26 (BL-3887 Notes): <c>curl -sS smb://127.0.0.1:20002/share/x</c> exits 1 with
/// <c>curl: (1) Protocol "smb" is disabled</c> and connects nowhere.
/// </summary>
[TestClass]
public sealed class DisabledProtocolHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("smb://127.0.0.1:20002/share/x", "Protocol \"smb\" is disabled")]
    [DataRow("SMBS://127.0.0.1:20002/share/x", "Protocol \"smbs\" is disabled")]
    public async Task ExecuteAsync_AnyUrl_FailsWithExitOneAndTheDisabledLine(string url, string expected)
    {
        InfoRecordingEvents events = new();
        DisabledProtocolHandler handler = new(["smb", "smbs"]);
        Diagnostics.Arrange("url", url);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = Stream.Null, Events = events });

        Diagnostics.Assert("exit code and message", $"{CurlExitCode.UnsupportedProtocol} {expected}", $"{result.ExitCode} {result.ErrorMessage}");
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { expected }, events.InfoLines);
    }

    [TestMethod]
    public void SupportedSchemes_IsTheSchemesGiven()
    {
        DisabledProtocolHandler handler = new(["smb", "smbs"]);

        Diagnostics.Assert("schemes", "smb,smbs", string.Join(",", handler.SupportedSchemes));
        CollectionAssert.AreEqual(new[] { "smb", "smbs" }, handler.SupportedSchemes.ToArray());
    }

    private sealed class InfoRecordingEvents : ITransferEvents
    {
        public List<string> InfoLines { get; } = [];

        public void ReportInfo(string text) => InfoLines.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
