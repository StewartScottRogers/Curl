using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-062 (BL-3591, ADR-0829): <c>%{redirect_url}</c>, and so <c>%{url_effective}</c> once
/// followed, is the <c>Location</c> as curl 8.22.0 normalises it - the host after a single slash,
/// a <c>%2E</c> dot segment removed and, on Windows, a drive-letter <c>file</c> path without the
/// slash before it.
/// </summary>
[TestClass]
public sealed class Cgr062Tests
{
    private static readonly CurlUrl RequestUrl = CurlUrl.Parse("http://127.0.0.1:20200/a/b");

    [TestMethod]
    [DataRow("https:/x", "https://x/", DisplayName = "one slash, https")]
    [DataRow("http:/b", "http://b/", DisplayName = "one slash, http")]
    [DataRow("HTTP:///b/c", "http://b/c", DisplayName = "three slashes")]
    [DataRow("/a/%252e%252e/b", "http://127.0.0.1:20200/a/%252e%252e/b", DisplayName = "escaped percent is no dot")]
    [DataRow("/a/%2e%2e/b", "http://127.0.0.1:20200/b", DisplayName = "relative, escaped dots")]
    [DataRow("/a/.%2E/b/%2e", "http://127.0.0.1:20200/b/", DisplayName = "relative, mixed dots")]
    [DataRow("http://h/a/%2E%2e/b", "http://h/b", DisplayName = "absolute, escaped dots")]
    [DataRow("http:", "http:", DisplayName = "no slash, not a URL")]
    public void Resolve_UnusualLocation_IsCurlsNormalisedUrl(string location, string expected)
    {
        string resolved = HttpRedirectLocation.Resolve(RequestUrl, location, driveLetters: false);

        Assert.AreEqual(expected, resolved);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Resolve_FileLocationNamingADrive_DropsTheSlashBeforeTheDriveOnWindows()
    {
        string resolved = HttpRedirectLocation.Resolve(RequestUrl, "file:///c:/windows/win.ini", driveLetters: true);

        Assert.AreEqual("file://c:/windows/win.ini", resolved);
    }

    [TestMethod]
    public void Resolve_FileLocationNamingADriveWithoutDriveLetters_IsUnchanged()
    {
        string resolved = HttpRedirectLocation.Resolve(RequestUrl, "file:///c:/windows/win.ini", driveLetters: false);

        Assert.AreEqual("file:///c:/windows/win.ini", resolved);
    }

    [TestMethod]
    public void Resolve_FileLocationWithoutADrive_IsUnchanged()
    {
        string resolved = HttpRedirectLocation.Resolve(RequestUrl, "file:///etc/x", driveLetters: true);

        Assert.AreEqual("file:///etc/x", resolved);
    }
}
