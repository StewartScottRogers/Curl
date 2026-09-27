namespace Curl.Cli;

/// <summary>
/// Pins <see cref="UploadTransferUrl.TryResolve"/> against curl 8.21.0's <c>%{url_effective}</c>
/// for <c>-T local.txt &lt;url&gt;</c>, measured on 2026-09-27 (BL-030 Notes).
/// </summary>
[TestClass]
public sealed class UploadTransferUrlTests
{
    [TestMethod]
    [DataRow("http:/host", "http://host/local.txt")]
    [DataRow("host/dir/", "http://host/dir/local.txt")]
    [DataRow("HTTP://User:pw@h:8080/d/", "http://User:pw@h:8080/d/local.txt")]
    [DataRow("http://h/d/#frag", "http://h/d/local.txt#frag")]
    [DataRow("http://h/d/?", "http://h/d/local.txt")]
    [DataRow("http://h/d/?q=1", "http://h/d/?q=1")]
    [DataRow("http://h/a/../d/", "http://h/d/local.txt")]
    [DataRow("http://[::1]:1/d/", "http://[::1]:1/d/local.txt")]
    [DataRow("http://[fe80::1%25eth0]/d/", "http://[fe80::1%25eth0]/d/local.txt")]
    [DataRow("http://h", "http://h/local.txt")]
    [DataRow("ftp://h/x/", "ftp://h/x/local.txt")]
    [DataRow("http://h/x", "http://h/x")]
    [DataRow("imap://u;AUTH=PLAIN@h/", "imap://u;AUTH=PLAIN@h/local.txt")]
    [DataRow("http://u@h/", "http://u@h/local.txt")]
    public void TryResolve_FileUpload_AppendsTheNameAndNormalises(string url, string expected)
    {
        bool resolved = UploadTransferUrl.TryResolve(url, "local.txt", out string transferUrl);

        Assert.IsTrue(resolved);
        Assert.AreEqual(expected, transferUrl);
    }

    [TestMethod]
    public void TryResolve_FileUrl_WritesThePathAfterThreeSlashes()
    {
        bool resolved = UploadTransferUrl.TryResolve("file:///tmp/", "local.txt", out string transferUrl);

        Assert.IsTrue(resolved);
        Assert.AreEqual("file:///tmp/local.txt", transferUrl);
    }

    [TestMethod]
    public void TryResolve_FileUrlWithDriveLetter_KeepsTheSlashBeforeItOnWindowsAndFailsElsewhere()
    {
        bool resolved = UploadTransferUrl.TryResolve("file:///C:/tmp/", "local.txt", out string transferUrl);

        Assert.AreEqual(OperatingSystem.IsWindows(), resolved);
        Assert.AreEqual(OperatingSystem.IsWindows() ? "file:///C:/tmp/local.txt" : string.Empty, transferUrl);
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow(".")]
    public void TryResolve_StandardInput_LeavesTheUrlUnchanged(string uploadFile)
    {
        bool resolved = UploadTransferUrl.TryResolve("http://h/d ir/", uploadFile, out string transferUrl);

        Assert.IsTrue(resolved);
        Assert.AreEqual("http://h/d ir/", transferUrl);
    }

    [TestMethod]
    [DataRow("http://h/d ir/")]
    [DataRow("http://h/d ir/x")]
    public void TryResolve_MalformedUrl_FailsWithAnEmptyUrl(string url)
    {
        bool resolved = UploadTransferUrl.TryResolve(url, "nosuchfile", out string transferUrl);

        Assert.IsFalse(resolved);
        Assert.AreEqual(string.Empty, transferUrl);
    }

    [TestMethod]
    public void TryResolve_NullUrl_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UploadTransferUrl.TryResolve(null!, "a", out _));

    [TestMethod]
    public void TryResolve_NullUrlFromStandardInput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UploadTransferUrl.TryResolve(null!, "-", out _));

    [TestMethod]
    public void TryResolve_NullUploadFile_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UploadTransferUrl.TryResolve("http://h/", null!, out _));
}
