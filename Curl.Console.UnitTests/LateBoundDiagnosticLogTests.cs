using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="LateBoundDiagnosticLog" /> (BL-923): nothing is enabled before it is bound, and
/// every line goes to the bound log after.
/// </summary>
[TestClass]
public sealed class LateBoundDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void IsEnabled_BeforeBind_IsFalse()
    {
        LateBoundDiagnosticLog log = new();
        Diagnostics.Arrange("bound", false);

        log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Auth, "dropped");
        bool enabled = log.IsEnabled(DiagnosticLogLevel.Error);

        Diagnostics.Act("IsEnabled(Error)", enabled);

        Diagnostics.Assert("IsEnabled(Error)", false, enabled);
        Assert.IsFalse(log.IsEnabled(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public void Write_AfterBind_ForwardsToTheBoundLog()
    {
        LateBoundDiagnosticLog log = new();
        RecordingLog bound = new();
        Diagnostics.Arrange("bound", true);

        log.Bind(bound);
        log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Auth, "chose Digest");
        bool enabled = log.IsEnabled(DiagnosticLogLevel.Verbose);

        Diagnostics.Act("IsEnabled(Verbose)", enabled);
        Diagnostics.Act("lines", string.Join("|", bound.Lines));

        Diagnostics.Assert("IsEnabled(Verbose)", true, enabled);
        Assert.IsTrue(log.IsEnabled(DiagnosticLogLevel.Verbose));
        Diagnostics.Assert("lines", "info auth chose Digest", string.Join("|", bound.Lines));
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
