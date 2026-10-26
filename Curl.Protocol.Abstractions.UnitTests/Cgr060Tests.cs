namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins CGR-060 (BL-3589): curl keeps every backslash in a <c>file</c> URL as typed (curl 8.21.0
/// Schannel and 8.18.0 mingw, measured: <c>file:///c:\nonexist\zz.txt</c> fails to open
/// <c>c:\nonexist\zz.txt</c>, <c>file:///tmp/no\such\file</c> fails to open <c>/tmp/no\such\file</c>,
/// and <c>file://\server\share</c> is <c>Bad file:// URL</c>), so <see cref="CurlUrl" /> neither turns
/// them into slashes nor takes them as path separators for dot-segment removal.
/// </summary>
[TestClass]
public sealed class Cgr060Tests
{
    [TestMethod]
    [DataRow("file:///c:\\nonexist\\zz.txt", true, "c:\\nonexist\\zz.txt")]
    [DataRow("file://c:\\nonexist\\zz.txt", true, "c:\\nonexist\\zz.txt")]
    [DataRow("file:///C:/dir\\..\\x", true, "C:/dir\\..\\x")]
    [DataRow("file:///tmp/no\\such\\file", false, "/tmp/no\\such\\file")]
    [DataRow("file://localhost/tmp/a\\b", false, "/tmp/a\\b")]
    public void TryParse_FileUrlWithBackslashes_KeepsThemInThePath(string text, bool driveLetters, string path)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters, out CurlUrl? url);

        Assert.IsTrue(parsed, text);
        Assert.AreEqual(path, url!.AbsolutePath);
    }

    [TestMethod]
    [DataRow("file://\\server\\share")]
    [DataRow("file://\\C:/x")]
    [DataRow("file://\\\\C:/x")]
    [DataRow("file://localhost\\tmp/dir/../nosuch.txt")]
    [DataRow("file://localhost\\C:/dir/../nosuch.txt")]
    public void TryParse_FileUrlWithBackslashAuthority_IsRejected(string text)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out CurlUrl? url);

        Assert.IsFalse(parsed, text);
        Assert.IsNull(url);
    }
}
