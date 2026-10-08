using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the name <c>-O</c> takes from a URL path, as measured with curl 8.21.0 on 2026-09-27
/// (BL-239 Notes).
/// </summary>
[TestClass]
public sealed class RemoteFileNameTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromUrlPath_FilePath_GivesTheLastSegment() =>
        Assert.AreEqual("file.txt", NameFrom("/dir/file.txt", "file.txt"));

    [TestMethod]
    public void FromUrlPath_TrailingSlashes_GivesTheLastSegmentThatIsNotEmpty() =>
        Assert.AreEqual("b", NameFrom("/a/b//", "b"));

    [TestMethod]
    public void FromUrlPath_FileUrlPathWithDrive_GivesTheFileName() =>
        Assert.AreEqual("win.ini", NameFrom("C:/Windows/win.ini", "win.ini"));

    [TestMethod]
    public void FromUrlPath_Root_GivesNull() =>
        Assert.IsNull(NameFrom("/", null));

    [TestMethod]
    public void FromUrlPath_Empty_GivesNull() =>
        Assert.IsNull(NameFrom(string.Empty, null));

    private string? NameFrom(string urlPath, string? expected)
    {
        Diagnostics.Arrange("url path", urlPath);
        string? name = RemoteFileName.FromUrlPath(urlPath);
        Diagnostics.Act("remote file name", name ?? "null");
        Diagnostics.Assert("remote file name", expected ?? "null", name ?? "null");
        return name;
    }
}
