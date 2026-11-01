using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins CGR-229 (BL-3758): on Windows curl 8.22.0 reads <c>file:///C%3A/Windows/win.ini</c>
/// as the drive path <c>C:\Windows\win.ini</c>, because it decodes the escape before it
/// looks for the drive letter; the slash in front of the drive goes the same as for
/// <c>file:///C:/...</c>.
/// </summary>
[TestClass]
public sealed class Cgr229Tests
{
    // curl -sS file:///C%3A/Windows/win.ini prints win.ini, exit 0 (Windows).
    [TestMethod]
    [DataRow("file:///C%3A/Windows/win.ini")]
    [DataRow("file:///C%3a/Windows/win.ini")]
    public void TryParse_EncodedDriveColonOnWindows_DropsTheSlashBeforeTheDrive(string url)
    {
        Assert.IsTrue(FileUrlPath.TryParse(CurlUrl.Parse(url), true, out var path));
        Assert.AreEqual("C:/Windows/win.ini", path.OsPath.Replace('\\', '/'));
    }

    [TestMethod]
    public void TryParse_EncodedDriveColonOffWindows_KeepsTheSlash()
    {
        Assert.IsTrue(FileUrlPath.TryParse(CurlUrl.Parse("file:///C%3A/Windows/win.ini"), false, out var path));
        Assert.IsTrue(path.OsPath.Replace('\\', '/').StartsWith("/C:", StringComparison.Ordinal));
    }
}
