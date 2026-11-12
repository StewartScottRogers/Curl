using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>Pins how extension warnings reach standard error as <c>Warning:</c> lines.</summary>
[TestClass]
public sealed class ExtensionWarningDiagnosticLogTests
{
    [TestMethod]
    public void Write_AnExtensionWarningTheInnerLogDoesNotTake_IsWrittenAsAWarningLine()
    {
        var inner = new StubLog(false);
        var lines = new List<string>();
        var log = new ExtensionWarningDiagnosticLog(inner, lines.Add);

        log.Write(DiagnosticLogLevel.Error, "code-generation", "Failed to open x to write code");
        log.Write(DiagnosticLogLevel.Warning, "otel", "export failed");

        CollectionAssert.AreEqual(new[] { "Warning: Failed to open x to write code", "Warning: export failed" }, lines);
        Assert.IsEmpty(inner.Lines);
    }

    [TestMethod]
    public void Write_TheInnerLogTakesTheLevel_LeavesItToTheInnerLog()
    {
        var inner = new StubLog(true);
        var lines = new List<string>();
        var log = new ExtensionWarningDiagnosticLog(inner, lines.Add);

        log.Write(DiagnosticLogLevel.Warning, "otel", "export failed");

        Assert.IsEmpty(lines);
        Assert.HasCount(1, inner.Lines);
    }

    [TestMethod]
    public void Write_AnotherComponentOrAnotherLevel_GoesToTheInnerLog()
    {
        var inner = new StubLog(false);
        var lines = new List<string>();
        var log = new ExtensionWarningDiagnosticLog(inner, lines.Add);

        log.Write(DiagnosticLogLevel.Warning, "runner", "x");
        log.Write(DiagnosticLogLevel.Info, "otel", "y");

        Assert.IsEmpty(lines);
        Assert.HasCount(2, inner.Lines);
        Assert.IsFalse(log.IsEnabled(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void OverStandardError_AWarning_IsWrittenToTheStreamAsALine()
    {
        using var stream = new MemoryStream();
        var log = ExtensionWarningDiagnosticLog.OverStandardError(new StubLog(false), () => stream, System.Text.Encoding.UTF8);

        log.Write(DiagnosticLogLevel.Warning, "otel", "bad");

        Assert.AreEqual("Warning: bad" + Environment.NewLine, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private sealed class StubLog(bool enabled) : IDiagnosticLog
    {
        public List<string> Lines { get; } = [];

        public bool IsEnabled(DiagnosticLogLevel level) => enabled;

        public void Write(DiagnosticLogLevel level, string component, string message) => Lines.Add(message);
    }
}
