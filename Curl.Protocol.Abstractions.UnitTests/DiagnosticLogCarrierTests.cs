namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins where a transfer carries its diagnostic log (ADR-0222): every carrier defaults to
/// <see cref="NoDiagnosticLog.Instance" />, and an assigned log is the one returned.
/// </summary>
[TestClass]
public sealed class DiagnosticLogCarrierTests
{
    private static readonly CurlUrl AnyUrl = CurlUrl.Parse("http://example.com/");

    [TestMethod]
    public void ITransferContextDiagnosticLog_NotOverridden_IsNoDiagnosticLog()
    {
        var inner = new TransferContext { Url = AnyUrl, Output = Stream.Null, DiagnosticLog = new StubDiagnosticLog() };
        ITransferContext wrapper = new ForwardingTransferContext(inner);

        Assert.AreSame(NoDiagnosticLog.Instance, wrapper.DiagnosticLog);
        Assert.AreSame(AnyUrl, wrapper.Url);
    }

    [TestMethod]
    public void TransferContextDiagnosticLog_WhenNotSet_IsNoDiagnosticLog()
    {
        ITransferContext context = new TransferContext { Url = AnyUrl, Output = Stream.Null };

        Assert.AreSame(NoDiagnosticLog.Instance, context.DiagnosticLog);
    }

    [TestMethod]
    public void TransferContextDiagnosticLog_WhenSet_RoundTrips()
    {
        var log = new StubDiagnosticLog();

        ITransferContext context = new TransferContext { Url = AnyUrl, Output = Stream.Null, DiagnosticLog = log };

        Assert.AreSame(log, context.DiagnosticLog);
    }

    [TestMethod]
    public void ConnectTargetDiagnosticLog_WhenNotSet_IsNoDiagnosticLog()
    {
        var target = new ConnectTarget("example.com", 443, true);

        Assert.AreSame(NoDiagnosticLog.Instance, target.DiagnosticLog);
    }

    [TestMethod]
    public void ConnectTargetDiagnosticLog_WhenSet_RoundTrips()
    {
        var log = new StubDiagnosticLog();

        var target = new ConnectTarget("example.com", 443, true) { DiagnosticLog = log };

        Assert.AreSame(log, target.DiagnosticLog);
    }

    [TestMethod]
    public void StubDiagnosticLog_Write_RecordsTheLine()
    {
        var log = new StubDiagnosticLog();

        log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns, "resolved");

        Assert.IsTrue(log.IsEnabled(DiagnosticLogLevel.Verbose));
        CollectionAssert.AreEqual(new[] { "Info dns resolved" }, log.Lines);
    }
}
