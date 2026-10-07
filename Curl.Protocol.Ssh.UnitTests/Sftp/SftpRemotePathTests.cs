using System.Text;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpRemotePath" />: the decoding and home-directory rule measured
/// 2026-09-29 (BL-569, ADR-0220).
/// </summary>
[TestClass]
public sealed class SftpRemotePathTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("/a%20b.txt", "2F6120622E747874", DisplayName = "escaped space")]
    [DataRow("/x%2fy", "2F782F79", DisplayName = "lower-case escape")]
    [DataRow("/%FF", "2FFF", DisplayName = "escape above ASCII stays one byte")]
    [DataRow("/é", "2FC3A9", DisplayName = "unescaped character as UTF-8")]
    [DataRow("/100%", "2F31303025", DisplayName = "percent at the end")]
    [DataRow("/1%4", "2F312534", DisplayName = "one digit at the end")]
    [DataRow("/%zz", "2F257A7A", DisplayName = "not hex digits")]
    [DataRow("/%4g", "2F253467", DisplayName = "second not a hex digit")]
    public void Decode_UrlPath_TurnsEscapesIntoBytes(string urlPath, string expectedHex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("URL path", urlPath);

        byte[] decoded = SftpRemotePath.Decode(urlPath);
        diagnostics.Bytes("decoded path", decoded);
        diagnostics.Act("decoded path hex", Convert.ToHexString(decoded));

        diagnostics.Diff("decoded path", Convert.FromHexString(expectedHex), decoded);
        CollectionAssert.AreEqual(Convert.FromHexString(expectedHex), decoded);
    }

    [TestMethod]
    [DataRow("/d/", true, DisplayName = "trailing slash")]
    [DataRow("/d/x%2F", true, DisplayName = "escaped trailing slash, as measured")]
    [DataRow("/d/x%2f", true, DisplayName = "lower-case escaped trailing slash")]
    [DataRow("/", true, DisplayName = "root")]
    [DataRow("/d/f", false, DisplayName = "file")]
    [DataRow("/d/%2F%41", false, DisplayName = "escaped slash further in")]
    [DataRow("", false, DisplayName = "empty")]
    public void NamesDirectory_UrlPath_IsTrueWhenTheDecodedPathEndsWithASlash(string urlPath, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("URL path", urlPath);

        bool namesDirectory = SftpRemotePath.NamesDirectory(urlPath);
        diagnostics.Act("names a directory", namesDirectory);

        diagnostics.Assert("names a directory", expected, namesDirectory);
        Assert.AreEqual(expected, namesDirectory);
    }

    [TestMethod]
    [DataRow("/~", true, DisplayName = "tilde alone, the home directory")]
    [DataRow("/%7E", true, DisplayName = "escaped tilde alone")]
    [DataRow("/~x", false, DisplayName = "tilde and a name")]
    [DataRow("~", false, DisplayName = "tilde without the slash")]
    public void NamesDirectory_TildePath_IsTrueOnlyForTheHomeDirectory(string urlPath, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("URL path", urlPath);

        bool namesDirectory = SftpRemotePath.NamesDirectory(urlPath);
        diagnostics.Act("names a directory", namesDirectory);

        diagnostics.Assert("names a directory", expected, namesDirectory);
        Assert.AreEqual(expected, namesDirectory);
    }

    [TestMethod]
    [DataRow("/~/f", "/home/fake", "/home/fake/f", DisplayName = "home prefix")]
    [DataRow("/~/", "/home/fake", "/home/fake/", DisplayName = "home prefix alone")]
    [DataRow("/~", "/home/fake", "/home/fake/", DisplayName = "tilde alone, as measured")]
    [DataRow("/~/f", "/home/fake/", "/home/fake/f", DisplayName = "home ending with a slash gains no second one")]
    [DataRow("/~/", "/", "/", DisplayName = "root home prefix alone")]
    [DataRow("/~", "/", "//", DisplayName = "root home, tilde alone")]
    [DataRow("/~/f", "", "f", DisplayName = "empty home")]
    [DataRow("/~x", "/home/fake", "/~x", DisplayName = "tilde and a name")]
    [DataRow("/x/~/f", "/home/fake", "/x/~/f", DisplayName = "tilde further in")]
    public void Resolve_Path_ReplacesALeadingHomeDirectoryAsCurlDoes(string path, string homeDirectory, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);
        diagnostics.Arrange("home directory", homeDirectory);

        byte[] resolved = SftpRemotePath.Resolve(Encoding.UTF8.GetBytes(path), Encoding.UTF8.GetBytes(homeDirectory));
        string actual = Encoding.UTF8.GetString(resolved);
        diagnostics.Act("resolved path", actual);

        diagnostics.Diff("resolved path", expected, actual);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(resolved));
    }

    [TestMethod]
    [DataRow("/%7E/a%20b", "/home/fake/a b", DisplayName = "escaped tilde and space")]
    [DataRow("/~", "/home/fake/", DisplayName = "tilde alone")]
    public void ResolveUrlPath_UrlPath_DecodesThenResolves(string urlPath, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("URL path", urlPath);
        diagnostics.Arrange("home directory", "/home/fake");

        byte[] resolved = SftpRemotePath.ResolveUrlPath(urlPath, Encoding.UTF8.GetBytes("/home/fake"));
        string actual = Encoding.UTF8.GetString(resolved);
        diagnostics.Act("resolved path", actual);

        diagnostics.Diff("resolved path", expected, actual);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(resolved));
    }

    [TestMethod]
    [DataRow("/~/bl572/files/a%00.txt", DisplayName = "as measured")]
    [DataRow("/%00", DisplayName = "zero byte alone")]
    public void ResolveUrlPath_ZeroByte_ThrowsExit3AsMeasured(string urlPath)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("URL path", urlPath);

        SshTransferException failure = Assert.ThrowsExactly<SshTransferException>(
            () => SftpRemotePath.ResolveUrlPath(urlPath, Encoding.UTF8.GetBytes("/home/fake")));
        diagnostics.Act("failure", $"exit {failure.ExitCode}: {failure.Message} (verbose line {failure.IsVerboseLine})");

        diagnostics.Assert("exit code", Curl.Protocol.Abstractions.CurlExitCode.UrlMalformat, failure.ExitCode);
        diagnostics.Diff("message", "URL using bad/illegal format or missing URL", failure.Message);
        Assert.AreEqual(Curl.Protocol.Abstractions.CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("URL using bad/illegal format or missing URL", failure.Message);
        Assert.IsFalse(failure.IsVerboseLine);
    }
}
