using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="UploadTransferUrl.TryResolve"/> against curl 8.21.0's <c>%{url_effective}</c>
/// for <c>-T local.txt &lt;url&gt;</c>, measured on 2026-09-27 (BL-030 Notes).
/// </summary>
[TestClass]
public sealed class UploadTransferUrlTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        ArrangeUpload(url, "local.txt");

        bool resolved = UploadTransferUrl.TryResolve(url, "local.txt", out string transferUrl);

        ActResolve(resolved, transferUrl);
        Diagnostics.Assert("resolved", true, resolved);
        Diagnostics.Diff("transfer URL", expected, transferUrl);
        Assert.IsTrue(resolved);
        Assert.AreEqual(expected, transferUrl);
    }

    [TestMethod]
    public void TryResolve_FileUrl_WritesThePathAfterThreeSlashes()
    {
        ArrangeUpload("file:///tmp/", "local.txt");

        bool resolved = UploadTransferUrl.TryResolve("file:///tmp/", "local.txt", out string transferUrl);

        ActResolve(resolved, transferUrl);
        Diagnostics.Assert("resolved", true, resolved);
        Diagnostics.Diff("transfer URL", "file:///tmp/local.txt", transferUrl);
        Assert.IsTrue(resolved);
        Assert.AreEqual("file:///tmp/local.txt", transferUrl);
    }

    [TestMethod]
    public void TryResolve_FileUrlWithDriveLetter_KeepsTheSlashBeforeItOnWindowsAndFailsElsewhere()
    {
        ArrangeUpload("file:///C:/tmp/", "local.txt");

        bool resolved = UploadTransferUrl.TryResolve("file:///C:/tmp/", "local.txt", out string transferUrl);

        // The answer differs by platform on purpose, so only whether it is this platform's answer is written.
        bool isThisPlatformsAnswer = resolved == OperatingSystem.IsWindows()
            && transferUrl == (OperatingSystem.IsWindows() ? "file:///C:/tmp/local.txt" : string.Empty);
        Diagnostics.Act("is this platform's answer", isThisPlatformsAnswer);
        Diagnostics.Assert("is this platform's answer", true, isThisPlatformsAnswer);
        Assert.AreEqual(OperatingSystem.IsWindows(), resolved);
        Assert.AreEqual(OperatingSystem.IsWindows() ? "file:///C:/tmp/local.txt" : string.Empty, transferUrl);
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow(".")]
    public void TryResolve_StandardInput_LeavesTheUrlUnchanged(string uploadFile)
    {
        ArrangeUpload("http://h/d ir/", uploadFile);

        bool resolved = UploadTransferUrl.TryResolve("http://h/d ir/", uploadFile, out string transferUrl);

        ActResolve(resolved, transferUrl);
        Diagnostics.Assert("resolved", true, resolved);
        Diagnostics.Diff("transfer URL", "http://h/d ir/", transferUrl);
        Assert.IsTrue(resolved);
        Assert.AreEqual("http://h/d ir/", transferUrl);
    }

    [TestMethod]
    [DataRow("http://h/d ir/")]
    [DataRow("http://h/d ir/x")]
    public void TryResolve_MalformedUrl_FailsWithAnEmptyUrl(string url)
    {
        ArrangeUpload(url, "nosuchfile");

        bool resolved = UploadTransferUrl.TryResolve(url, "nosuchfile", out string transferUrl);

        ActResolve(resolved, transferUrl);
        Diagnostics.Assert("resolved", false, resolved);
        Diagnostics.Diff("transfer URL", string.Empty, transferUrl);
        Assert.IsFalse(resolved);
        Assert.AreEqual(string.Empty, transferUrl);
    }

    [TestMethod]
    public void TryResolve_NullUrl_Throws()
    {
        ArrangeUpload(null, "a");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => UploadTransferUrl.TryResolve(null!, "a", out _));

        ActAndAssertThrown(exception);
    }

    [TestMethod]
    public void TryResolve_NullUrlFromStandardInput_Throws()
    {
        ArrangeUpload(null, "-");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => UploadTransferUrl.TryResolve(null!, "-", out _));

        ActAndAssertThrown(exception);
    }

    [TestMethod]
    public void TryResolve_NullUploadFile_Throws()
    {
        ArrangeUpload("http://h/", null);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => UploadTransferUrl.TryResolve("http://h/", null!, out _));

        ActAndAssertThrown(exception);
    }

    private void ArrangeUpload(string? url, string? uploadFile)
    {
        Diagnostics.Arrange("url", CommandLineParseDiagnostics.QuoteEach([url]));
        Diagnostics.Arrange("upload file", CommandLineParseDiagnostics.QuoteEach([uploadFile]));
    }

    private void ActResolve(bool resolved, string transferUrl)
    {
        Diagnostics.Act("resolved", resolved);
        Diagnostics.Act("transfer URL", "\"" + transferUrl + "\"");
    }

    private void ActAndAssertThrown(ArgumentNullException exception)
    {
        Diagnostics.Act("exception", exception.GetType().Name + " for " + exception.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }
}
