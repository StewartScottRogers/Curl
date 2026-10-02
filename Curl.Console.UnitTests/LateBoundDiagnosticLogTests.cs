using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="LateBoundDiagnosticLog" /> (BL-923): nothing is enabled before it is bound, and
/// every line goes to the bound log after.
/// </summary>
[TestClass]
public sealed class LateBoundDiagnosticLogTests
{
    [TestMethod]
    public void IsEnabled_BeforeBind_IsFalse()
    {
        LateBoundDiagnosticLog log = new();

        log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Auth, "dropped");

        Assert.IsFalse(log.IsEnabled(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public void Write_AfterBind_ForwardsToTheBoundLog()
    {
        LateBoundDiagnosticLog log = new();
        RecordingLog bound = new();

        log.Bind(bound);
        log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Auth, "chose Digest");

        Assert.IsTrue(log.IsEnabled(DiagnosticLogLevel.Verbose));
        CollectionAssert.AreEqual(new[] { "info auth chose Digest" }, bound.Lines);
    }

    private sealed class RecordingLog : IDiagnosticLog
    {
        public List<string> Lines { get; } = [];

        public bool IsEnabled(DiagnosticLogLevel level) => true;

        public void Write(DiagnosticLogLevel level, string component, string message) =>
            Lines.Add($"{level.ToString().ToLowerInvariant()} {component} {message}");
    }
}
