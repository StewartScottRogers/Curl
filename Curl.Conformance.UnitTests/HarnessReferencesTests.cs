using System.Reflection;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins the two assemblies the conformance harness is built from: the harness library
/// itself and the <c>curl</c> runner it drives (ADR-0013, decisions 2 and 4). Both must
/// be copied beside these tests for any upstream case to run.
/// </summary>
[TestClass]
public sealed class HarnessReferencesTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HarnessLibraryLoadsBesideTheTests()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("assembly to load", "Curl.Conformance.UnitLibrary");

        Assembly harness = Assembly.Load("Curl.Conformance.UnitLibrary");

        string? name = harness.GetName().Name;
        diagnostics.Act("loaded assembly name", name);
        diagnostics.Assert("assembly name", "Curl.Conformance.UnitLibrary", name);
        Assert.AreEqual("Curl.Conformance.UnitLibrary", name);
    }

    [TestMethod]
    public void CurlRunnerLoadsBesideTheTests()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("assembly to load", "curl");

        Assembly runner = Assembly.Load("curl");

        string? name = runner.GetName().Name;
        diagnostics.Act("loaded assembly name", name);
        diagnostics.Assert("assembly name", "curl", name);
        Assert.AreEqual("curl", name);
    }
}
