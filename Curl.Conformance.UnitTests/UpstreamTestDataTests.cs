using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins the upstream test data vendored under <c>UpstreamTestData/</c> (ADR-0013, decision 3):
/// every <c>tests/data/test*</c> file of curl 8.21.0, stored here as <c>test*.rawhttp</c>, and curl's
/// <c>COPYING</c> notice are
/// copied beside the tests, so no case ever has to download anything.
/// </summary>
[TestClass]
public sealed class UpstreamTestDataTests
{
    /// <summary>The number of <c>test*</c> files in <c>tests/data</c> at <c>curl-8_21_0</c>.</summary>
    private const int VendoredTestFileCount = 2013;

    private static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_HoldsEveryTestFileFromTheTag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pattern", "UpstreamTestData/test*.rawhttp");
        diagnostics.Arrange("expected file count", VendoredTestFileCount);

        string[] testFiles = Directory.GetFiles(UpstreamTestDataFolder, "test*.rawhttp");

        diagnostics.Act("file count", testFiles.Length);
        diagnostics.Assert("file count", VendoredTestFileCount, testFiles.Length);
        Assert.HasCount(VendoredTestFileCount, testFiles);
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_CarriesCurlsCopyingNotice()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string copyingPath = Path.Combine(UpstreamTestDataFolder, "COPYING");
        diagnostics.Arrange("file", "UpstreamTestData/COPYING");

        string copying = File.ReadAllText(copyingPath);

        diagnostics.Act("length in characters", copying.Length);
        diagnostics.Assert("mentions Daniel Stenberg", true, copying.Contains("Daniel Stenberg", StringComparison.Ordinal));
        Assert.Contains("Daniel Stenberg", copying);
    }
}
