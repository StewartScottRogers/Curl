using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins where a transfer carries its diagnostic log (ADR-0222): every carrier defaults to
/// <see cref="NoDiagnosticLog.Instance" />, and an assigned log is the one returned.
/// </summary>
[TestClass]
public sealed class DiagnosticLogCarrierTests
{
    private static readonly CurlUrl AnyUrl = CurlUrl.Parse("http://example.com/");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ITransferContextDiagnosticLog_NotOverridden_IsNoDiagnosticLog()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var inner = new TransferContext { Url = AnyUrl, Output = Stream.Null, DiagnosticLog = new StubDiagnosticLog() };
        ITransferContext wrapper = new ForwardingTransferContext(inner);
        diagnostics.Arrange("url", AnyUrl);

        IDiagnosticLog log = wrapper.DiagnosticLog;

        diagnostics.Act("diagnostic log type", log.GetType().Name);
        diagnostics.Assert("diagnostic log", NoDiagnosticLog.Instance, log);
        Assert.AreSame(NoDiagnosticLog.Instance, wrapper.DiagnosticLog);
        Assert.AreSame(AnyUrl, wrapper.Url);
    }

    [TestMethod]
    public void TransferContextDiagnosticLog_WhenNotSet_IsNoDiagnosticLog()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", AnyUrl);
        ITransferContext context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        diagnostics.Act("diagnostic log type", context.DiagnosticLog.GetType().Name);
        diagnostics.Assert("diagnostic log", NoDiagnosticLog.Instance, context.DiagnosticLog);
        Assert.AreSame(NoDiagnosticLog.Instance, context.DiagnosticLog);
    }

    [TestMethod]
    public void TransferContextDiagnosticLog_WhenSet_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var log = new StubDiagnosticLog();
        diagnostics.Arrange("assigned log", log.GetType().Name);

        ITransferContext context = new TransferContext { Url = AnyUrl, Output = Stream.Null, DiagnosticLog = log };

        diagnostics.Act("diagnostic log type", context.DiagnosticLog.GetType().Name);
        diagnostics.Assert("diagnostic log", log, context.DiagnosticLog);
        Assert.AreSame(log, context.DiagnosticLog);
    }

    [TestMethod]
    public void ConnectTargetDiagnosticLog_WhenNotSet_IsNoDiagnosticLog()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var target = new ConnectTarget("example.com", 443, true);
        diagnostics.Arrange("target", target);

        diagnostics.Act("diagnostic log type", target.DiagnosticLog.GetType().Name);
        diagnostics.Assert("diagnostic log", NoDiagnosticLog.Instance, target.DiagnosticLog);
        Assert.AreSame(NoDiagnosticLog.Instance, target.DiagnosticLog);
    }

    [TestMethod]
    public void ConnectTargetDiagnosticLog_WhenSet_RoundTrips()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var log = new StubDiagnosticLog();
        diagnostics.Arrange("assigned log", log.GetType().Name);

        var target = new ConnectTarget("example.com", 443, true) { DiagnosticLog = log };

        diagnostics.Act("diagnostic log type", target.DiagnosticLog.GetType().Name);
        diagnostics.Assert("diagnostic log", log, target.DiagnosticLog);
        Assert.AreSame(log, target.DiagnosticLog);
    }

    [TestMethod]
    public void StubDiagnosticLog_Write_RecordsTheLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var log = new StubDiagnosticLog();
        diagnostics.Arrange("write", "Info dns resolved");

        log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns, "resolved");

        diagnostics.Act("lines", string.Join("|", log.Lines));
        diagnostics.Act("verbose enabled", log.IsEnabled(DiagnosticLogLevel.Verbose));
        diagnostics.Assert("verbose enabled", true, log.IsEnabled(DiagnosticLogLevel.Verbose));
        Assert.IsTrue(log.IsEnabled(DiagnosticLogLevel.Verbose));
        CollectionAssert.AreEqual(new[] { "Info dns resolved" }, log.Lines);
    }
}
