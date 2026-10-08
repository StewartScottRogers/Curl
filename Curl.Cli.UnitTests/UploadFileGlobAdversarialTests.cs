using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Adversarial black-box tests of <see cref="UploadFileGlob"/> (BL-1493): ranges at their limits, step zero,
/// unterminated, nested, empty and unmatched braces and brackets. Every pinned answer was measured with curl
/// 8.21.0 (mingw, Schannel) on 2026-10-07 with <c>curl -q -s -S -T '&lt;glob&gt;' http://127.0.0.1:1/g/</c>.
/// </summary>
[TestClass]
public sealed class UploadFileGlobAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("f[1-1]", "f1")]
    [DataRow("f[0-0]", "f0")]
    public void TryParse_SingleValueRange_ExpandsToOneFile(string uploadFile, string expectedFile)
    {
        UploadFileGlob glob = ParseAccepted(uploadFile);

        string[] files = [.. glob.ExpandUploadFiles()];

        Diagnostics.Assert("files", CommandLineParseDiagnostics.QuoteEach([expectedFile]), CommandLineParseDiagnostics.QuoteEach(files));
        CollectionAssert.AreEqual(new[] { expectedFile }, files);
    }

    [TestMethod]
    public void TryParse_RangeOfAHundredMillion_CountsWithoutExpandingAndYieldsTheFirstLazily()
    {
        UploadFileGlob glob = ParseAccepted("f[1-100000000]");

        string first = glob.ExpandUploadFiles().First();

        Diagnostics.Assert("UploadFileCount", 100000000L, glob.UploadFileCount);
        Assert.AreEqual(100000000L, glob.UploadFileCount);
        Diagnostics.Assert("first file", "f1", first);
        Assert.AreEqual("f1", first);
    }

    [TestMethod]
    [DataRow("f[a-z:0]", "bad range in position 9:\nf[a-z:0]\n        ^")]
    [DataRow("f[1-3:0]", "bad range in position 9:\nf[1-3:0]\n        ^")]
    [DataRow("f[1-", "bad range in position 5:\nf[1-\n    ^")]
    [DataRow("f[1-99999999999999999999]", "bad range in position 5:\nf[1-99999999999999999999]\n    ^")]
    [DataRow("f[-1-3]", "bad range specification in position 3:\nf[-1-3]\n  ^")]
    [DataRow("f{a,b", "unmatched brace in position 6:\nf{a,b\n     ^")]
    [DataRow("f{a,{b,c}}", "nested brace in position 5:\nf{a,{b,c}}\n    ^")]
    [DataRow("f{}", "empty string within braces in position 3:\nf{}\n  ^")]
    [DataRow("f]", "unmatched close brace/bracket in position 2:\nf]\n ^")]
    [DataRow("f}", "unmatched close brace/bracket in position 2:\nf}\n ^")]
    public void TryParse_MalformedGlob_RefusesAsUrlMalformatWithCurlsMessage(string uploadFile, string expectedMessage)
    {
        Diagnostics.Arrange("upload file", uploadFile);

        bool parsed = UploadFileGlob.TryParse(uploadFile, globOff: false, out UploadFileGlob? glob, out TransferResult? failure);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);
        Assert.IsNull(glob);
        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Diagnostics.Diff("error message", expectedMessage, failure.ErrorMessage ?? string.Empty);
        Assert.AreEqual(expectedMessage, failure.ErrorMessage);
    }

    private UploadFileGlob ParseAccepted(string uploadFile)
    {
        Diagnostics.Arrange("upload file", uploadFile);
        bool parsed = UploadFileGlob.TryParse(uploadFile, globOff: false, out UploadFileGlob? glob, out TransferResult? failure);
        Diagnostics.Act("parsed", parsed);
        Diagnostics.Act("error message", failure?.ErrorMessage ?? "none");
        Assert.IsTrue(parsed);
        return glob!;
    }
}
