namespace Curl.Conformance;

/// <summary>
/// Pins the upstream test data vendored under <c>UpstreamTestData/</c> (ADR-0013, decision 3):
/// every <c>tests/data/test*</c> file of curl 8.21.0 and curl's <c>COPYING</c> notice are
/// copied beside the tests, so no case ever has to download anything.
/// </summary>
[TestClass]
public sealed class UpstreamTestDataTests
{
    /// <summary>The number of <c>test*</c> files in <c>tests/data</c> at <c>curl-8_21_0</c>.</summary>
    private const int VendoredTestFileCount = 2013;

    private static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_HoldsEveryTestFileFromTheTag()
    {
        string[] testFiles = Directory.GetFiles(UpstreamTestDataFolder, "test*");

        Assert.HasCount(VendoredTestFileCount, testFiles);
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_CarriesCurlsCopyingNotice()
    {
        string copyingPath = Path.Combine(UpstreamTestDataFolder, "COPYING");

        string copying = File.ReadAllText(copyingPath);

        Assert.Contains("Daniel Stenberg", copying);
    }
}
