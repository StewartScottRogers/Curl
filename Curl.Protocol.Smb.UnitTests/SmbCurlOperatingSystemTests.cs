using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbCurlOperatingSystem" /> to the triple each platform's <c>curl -V</c> shows.
/// </summary>
[TestClass]
public sealed class SmbCurlOperatingSystemTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true, false, "x86_64-w64-mingw32")]
    [DataRow(false, true, "aarch64-apple-darwin25.0.0")]
    [DataRow(false, false, "x86_64-pc-linux-gnu")]
    public void For_IsTheVersionLinesTriple(bool isWindows, bool isMacOS, string expected)
    {
        Diagnostics.Arrange("platform", $"isWindows {isWindows}, isMacOS {isMacOS}");

        string triple = SmbCurlOperatingSystem.For(isWindows, isMacOS);

        Diagnostics.Act("triple", triple);
        Diagnostics.Diff("triple", expected, triple);
        Assert.AreEqual(expected, SmbCurlOperatingSystem.For(isWindows, isMacOS));
    }

    [TestMethod]
    public void Current_IsThisPlatformsTriple()
    {
        Diagnostics.Arrange("platform", $"isWindows {OperatingSystem.IsWindows()}, isMacOS {OperatingSystem.IsMacOS()}");
        string expected = SmbCurlOperatingSystem.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());

        string current = SmbCurlOperatingSystem.Current;

        Diagnostics.Act("current", current);
        Diagnostics.Diff("current", expected, current);
        Assert.AreEqual(SmbCurlOperatingSystem.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()), SmbCurlOperatingSystem.Current);
    }

}
