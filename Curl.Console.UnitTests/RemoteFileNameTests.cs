namespace Curl.Console;

/// <summary>
/// Pins the name <c>-O</c> takes from a URL path, as measured with curl 8.21.0 on 2026-09-27
/// (BL-239 Notes).
/// </summary>
[TestClass]
public sealed class RemoteFileNameTests
{
    [TestMethod]
    public void FromUrlPath_FilePath_GivesTheLastSegment() =>
        Assert.AreEqual("file.txt", RemoteFileName.FromUrlPath("/dir/file.txt"));

    [TestMethod]
    public void FromUrlPath_TrailingSlashes_GivesTheLastSegmentThatIsNotEmpty() =>
        Assert.AreEqual("b", RemoteFileName.FromUrlPath("/a/b//"));

    [TestMethod]
    public void FromUrlPath_FileUrlPathWithDrive_GivesTheFileName() =>
        Assert.AreEqual("win.ini", RemoteFileName.FromUrlPath("C:/Windows/win.ini"));

    [TestMethod]
    public void FromUrlPath_Root_GivesNull() =>
        Assert.IsNull(RemoteFileName.FromUrlPath("/"));

    [TestMethod]
    public void FromUrlPath_Empty_GivesNull() =>
        Assert.IsNull(RemoteFileName.FromUrlPath(string.Empty));
}
