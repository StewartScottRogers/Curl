namespace Curl.Cli;

[TestClass]
public sealed class Cgr230Tests
{
    // CGR-230: curl writes `-T upload.txt file:///NUL` to the NUL device; the third slash
    // starts the path, so the path names a file and no name is appended.
    [TestMethod]
    [DataRow("file:///NUL")]
    [DataRow("FILE:///nul")]
    [DataRow("file://localhost/NUL")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_FileUrlPathNamesAFile_ReturnsUrlUnchanged(string url)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "upload.txt");

        Assert.AreEqual(url, result);
    }

    [TestMethod]
    [DataRow("file:///dir/", "file:///dir/upload.txt")]
    [DataRow("file:///", "file:///upload.txt")]
    public void AppendLocalFileNameWhenUrlNamesNoFile_FileUrlPathIsADirectory_AppendsFileName(
        string url, string expected)
    {
        string result = UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile(url, "upload.txt");

        Assert.AreEqual(expected, result);
    }
}
