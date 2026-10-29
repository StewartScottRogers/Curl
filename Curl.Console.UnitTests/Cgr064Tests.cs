namespace Curl.Console;

/// <summary>
/// CGR-064 (BL-3593): curl takes the <c>-O</c> name from after the last backslash as well as
/// the last slash, so <c>/a\b.txt</c> saves <c>b.txt</c>, where Curl wrote to <c>a\b.txt</c>
/// (a missing folder on Windows, exit 23; a file of that name on Linux).
/// Measured on curl 8.21.0 (Schannel, Windows) on 2026-10-28.
/// </summary>
[TestClass]
public sealed class Cgr064Tests
{
    [TestMethod]
    [DataRow("/a\\b.txt", "b.txt")]
    [DataRow("/x/a\\", "a")]
    [DataRow("/x/a\\b\\c", "c")]
    [DataRow("/a\\b.txt/", "b.txt")]
    public void FromUrlPath_WithBackslash_ReturnsThePartAfterTheLastBackslash(string absolutePath, string expected) =>
        Assert.AreEqual(expected, RemoteFileName.FromUrlPath(absolutePath));
}
