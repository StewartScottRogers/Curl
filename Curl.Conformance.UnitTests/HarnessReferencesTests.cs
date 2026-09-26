using System.Reflection;

namespace Curl.Conformance;

/// <summary>
/// Pins the two assemblies the conformance harness is built from: the harness library
/// itself and the <c>curl</c> runner it drives (ADR-0013, decisions 2 and 4). Both must
/// be copied beside these tests for any upstream case to run.
/// </summary>
[TestClass]
public sealed class HarnessReferencesTests
{
    [TestMethod]
    public void HarnessLibraryLoadsBesideTheTests()
    {
        Assembly harness = Assembly.Load("Curl.Conformance.UnitLibrary");

        Assert.AreEqual("Curl.Conformance.UnitLibrary", harness.GetName().Name);
    }

    [TestMethod]
    public void CurlRunnerLoadsBesideTheTests()
    {
        Assembly runner = Assembly.Load("curl");

        Assert.AreEqual("curl", runner.GetName().Name);
    }
}
