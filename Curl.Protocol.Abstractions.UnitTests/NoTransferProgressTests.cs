using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that every member of <see cref="NoTransferProgress" /> does nothing (ADR-0045).
/// </summary>
[TestClass]
public sealed class NoTransferProgressTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EveryMember_Called_DoesNotThrow()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ITransferProgress progress = NoTransferProgress.Instance;
        diagnostics.Arrange("calls", "started, downloaded x2, uploaded x2, done");

        progress.ReportTransferStarted();
        progress.ReportDownloaded(10, 200000);
        progress.ReportDownloaded(10, null);
        progress.ReportUploaded(10, 200000);
        progress.ReportUploaded(10, null);
        progress.ReportTransferDone();

        diagnostics.Act("calls made", 6);
        diagnostics.Assert("calls made", 6, 6);
    }

    [TestMethod]
    public void ReportTransferDone_OnASinkThatDoesNotImplementIt_DoesNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        ITransferProgress progress = new StubTransferProgress();
        diagnostics.Arrange("sink", nameof(StubTransferProgress));

        progress.ReportTransferDone();

        diagnostics.Act("calls made", 1);
        diagnostics.Assert("calls made", 1, 1);
    }
}
