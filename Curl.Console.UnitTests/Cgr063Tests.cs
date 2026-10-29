namespace Curl.Console;

/// <summary>
/// CGR-063 (BL-3592): under <c>--path-as-is</c> the URL path keeps its dot segments, and curl
/// resolves them before it takes the <c>-O</c> name, so <c>/a/..</c> names no file and saves to
/// <c>curl_response</c>, where Curl wrote to <c>&lt;dir&gt;/..</c> and failed with exit 23.
/// Measured on curl 8.21.0 (Schannel, Windows) on 2026-10-28.
/// </summary>
[TestClass]
public sealed class Cgr063Tests
{
    [TestMethod]
    [DataRow("/a/..")]
    [DataRow("/a/../")]
    [DataRow("/..")]
    [DataRow("/a/%2e%2e")]
    [DataRow("/a/b/%2E./..")]
    public void FromUrlPath_WhenDotSegmentsLeaveNoName_ReturnsNull(string absolutePath) =>
        Assert.IsNull(RemoteFileName.FromUrlPath(absolutePath));

    [TestMethod]
    [DataRow("/a/.", "a")]
    [DataRow("/a/./", "a")]
    [DataRow("/a/b/%2E", "b")]
    [DataRow("/a/b/..", "a")]
    [DataRow("/a//..", "a")]
    [DataRow("/a/...", "...")]
    [DataRow("/a/.b", ".b")]
    public void FromUrlPath_WithDotSegments_ReturnsTheLastSegmentLeft(string absolutePath, string expected) =>
        Assert.AreEqual(expected, RemoteFileName.FromUrlPath(absolutePath));
}
