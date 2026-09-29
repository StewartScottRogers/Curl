namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbCurlOperatingSystem" /> to the triple each platform's <c>curl -V</c> shows.
/// </summary>
[TestClass]
public sealed class SmbCurlOperatingSystemTests
{
    [TestMethod]
    [DataRow(true, false, "x86_64-w64-mingw32")]
    [DataRow(false, true, "aarch64-apple-darwin25.0.0")]
    [DataRow(false, false, "x86_64-pc-linux-gnu")]
    public void For_IsTheVersionLinesTriple(bool isWindows, bool isMacOS, string expected)
    {
        Assert.AreEqual(expected, SmbCurlOperatingSystem.For(isWindows, isMacOS));
    }

    [TestMethod]
    public void Current_IsThisPlatformsTriple()
    {
        Assert.AreEqual(SmbCurlOperatingSystem.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()), SmbCurlOperatingSystem.Current);
    }

}
