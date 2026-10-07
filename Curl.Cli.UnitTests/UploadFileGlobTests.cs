using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="UploadFileGlob" /> against curl 8.21.0 (mingw, Schannel), measured on
/// 2026-09-27 with <c>curl -s -S -w '%{url_effective}\n' -T '&lt;glob&gt;' http://127.0.0.1:1/g/</c>
/// (BL-031 Notes).
/// </summary>
[TestClass]
public sealed class UploadFileGlobTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ResolveTransferTargets_SetGlob_SendsEachFileToItsOwnUrlInCurlsOrder()
    {
        UploadFileGlob glob = Parse("{local.txt,sub/in.txt}", globOff: false);

        UploadTransferTarget[] targets = ResolveTransferTargets(glob, "http://h/g/");

        UploadTransferTarget[] expected =
        [
            new UploadTransferTarget("local.txt", "http://h/g/local.txt", true),
            new UploadTransferTarget("sub/in.txt", "http://h/g/in.txt", true),
        ];
        Diagnostics.Assert("targets", Describe(expected), Describe(targets));
        CollectionAssert.AreEqual(
            new[]
            {
                new UploadTransferTarget("local.txt", "http://h/g/local.txt", true),
                new UploadTransferTarget("sub/in.txt", "http://h/g/in.txt", true),
            },
            targets);
    }

    [TestMethod]
    public void ResolveTransferTargets_RangeGlob_SendsEachFileInOrder()
    {
        UploadFileGlob glob = Parse("f[1-3].txt", globOff: false);

        string[] urls = [.. ResolveTransferTargets(glob, "http://h/g/").Select(target => target.TransferUrl)];

        string[] files = [.. glob.ExpandUploadFiles()];
        Diagnostics.Act("expanded files", CommandLineParseDiagnostics.QuoteEach(files));
        Diagnostics.Assert("upload file count", 3, glob.UploadFileCount);
        Diagnostics.Assert("expanded files", "[\"f1.txt\", \"f2.txt\", \"f3.txt\"]", CommandLineParseDiagnostics.QuoteEach(files));
        Diagnostics.Assert("urls", "[\"http://h/g/f1.txt\", \"http://h/g/f2.txt\", \"http://h/g/f3.txt\"]", CommandLineParseDiagnostics.QuoteEach(urls));
        Assert.AreEqual(3, glob.UploadFileCount);
        CollectionAssert.AreEqual(new[] { "f1.txt", "f2.txt", "f3.txt" }, glob.ExpandUploadFiles().ToArray());
        CollectionAssert.AreEqual(new[] { "http://h/g/f1.txt", "http://h/g/f2.txt", "http://h/g/f3.txt" }, urls);
    }

    [TestMethod]
    public void ResolveTransferTargets_EachFile_ResolvesAsUploadUrlAppendsIt()
    {
        UploadFileGlob glob = Parse("{a b.txt,c}", globOff: false);

        string[] urls = [.. ResolveTransferTargets(glob, "http://h/g/").Select(target => target.TransferUrl)];

        string[] expected =
        [
            UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile("http://h/g/", "a b.txt"),
            UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile("http://h/g/", "c"),
        ];
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(urls));
        Diagnostics.Assert("first url", "http://h/g/a%20b.txt", urls[0]);
        CollectionAssert.AreEqual(
            new[]
            {
                UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile("http://h/g/", "a b.txt"),
                UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile("http://h/g/", "c"),
            },
            urls);
        Assert.AreEqual("http://h/g/a%20b.txt", urls[0]);
    }

    [TestMethod]
    public void ResolveTransferTargets_GlobOff_UploadsTheLiteralNameOnce()
    {
        UploadFileGlob glob = Parse("{local.txt,sub/in.txt}", globOff: true);

        UploadTransferTarget[] targets = ResolveTransferTargets(glob, "http://h/g/");

        Diagnostics.Assert("upload file count", 1, glob.UploadFileCount);
        Diagnostics.Assert("targets", Describe([new UploadTransferTarget("{local.txt,sub/in.txt}", "http://h/g/in.txt%7D", true)]), Describe(targets));
        Assert.AreEqual(1, glob.UploadFileCount);
        Assert.HasCount(1, targets);
        Assert.AreEqual("{local.txt,sub/in.txt}", targets[0].UploadFile);
        Assert.AreEqual("http://h/g/in.txt%7D", targets[0].TransferUrl);
    }

    [TestMethod]
    public void ResolveTransferTargets_StandardInputAmongTheMatches_LeavesItsUrlUnchanged()
    {
        UploadFileGlob glob = Parse("{-,local.txt}", globOff: false);

        string[] urls = [.. ResolveTransferTargets(glob, "http://h/g/").Select(target => target.TransferUrl)];

        Diagnostics.Assert("urls", "[\"http://h/g/\", \"http://h/g/local.txt\"]", CommandLineParseDiagnostics.QuoteEach(urls));
        CollectionAssert.AreEqual(new[] { "http://h/g/", "http://h/g/local.txt" }, urls);
    }

    [TestMethod]
    public void ResolveTransferTargets_MalformedUrl_MarksTheTargetNotWellFormed()
    {
        UploadFileGlob glob = Parse("f[1-2]", globOff: false);

        UploadTransferTarget[] targets = ResolveTransferTargets(glob, "http://h/d ir/");

        Diagnostics.Assert("target count", 2, targets.Length);
        Diagnostics.Assert("first target well formed", false, targets[0].IsUrlWellFormed);
        Diagnostics.Assert("first target url", "\"\"", "\"" + targets[0].TransferUrl + "\"");
        Assert.HasCount(2, targets);
        Assert.IsFalse(targets[0].IsUrlWellFormed);
        Assert.AreEqual(string.Empty, targets[0].TransferUrl);
    }

    [TestMethod]
    public void ResolveTransferTargets_NullUrl_Throws()
    {
        UploadFileGlob glob = Parse("local.txt", globOff: false);
        Diagnostics.Arrange("url", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => glob.ResolveTransferTargets(null!).ToArray());

        ActAndAssertThrown(exception);
    }

    [TestMethod]
    public void TryParse_MalformedGlob_IsUrlMalformatWithCurlsMessageAboutTheUploadArgument()
    {
        ArrangeUploadFile("f[3-1].txt", globOff: false);

        bool parsed = UploadFileGlob.TryParse("f[3-1].txt", globOff: false, out UploadFileGlob? glob, out TransferResult? failure);

        ActTryParse(parsed, failure);
        Diagnostics.Assert("parsed", false, parsed);
        Diagnostics.Assert("exit code", (int)CurlExitCode.UrlMalformat, failure is null ? "none" : ((int)failure.ExitCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Diagnostics.Diff("error message", "bad range in position 7:\nf[3-1].txt\n      ^", failure?.ErrorMessage ?? string.Empty);
        Assert.IsFalse(parsed);

        Assert.IsNull(glob);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure!.ExitCode);
        Assert.AreEqual("bad range in position 7:\nf[3-1].txt\n      ^", failure.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_MalformedGlobUnderGlobOff_IsOneLiteralName()
    {
        ArrangeUploadFile("f[3-1].txt", globOff: true);

        bool parsed = UploadFileGlob.TryParse("f[3-1].txt", globOff: true, out UploadFileGlob? glob, out TransferResult? failure);

        ActTryParse(parsed, failure);
        string[] files = glob is null ? [] : [.. glob.ExpandUploadFiles()];
        Diagnostics.Act("expanded files", CommandLineParseDiagnostics.QuoteEach(files));
        Diagnostics.Assert("parsed", true, parsed);
        Diagnostics.Assert("expanded files", "[\"f[3-1].txt\"]", CommandLineParseDiagnostics.QuoteEach(files));
        Assert.IsTrue(parsed);

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { "f[3-1].txt" }, glob!.ExpandUploadFiles().ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryParse_NullUploadFile_Throws(bool globOff)
    {
        ArrangeUploadFile(null, globOff);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => UploadFileGlob.TryParse(null!, globOff, out _, out _));

        ActAndAssertThrown(exception);
    }

    private static string Describe(IEnumerable<UploadTransferTarget> targets) =>
        "[" + string.Join(", ", targets.Select(target => $"(\"{target.UploadFile}\" -> \"{target.TransferUrl}\", well formed {target.IsUrlWellFormed})")) + "]";

    private void ArrangeUploadFile(string? uploadFile, bool globOff)
    {
        Diagnostics.Arrange("upload file", CommandLineParseDiagnostics.QuoteEach([uploadFile]));
        Diagnostics.Arrange("glob off", globOff);
    }

    private void ActTryParse(bool parsed, TransferResult? failure)
    {
        Diagnostics.Act("parsed", parsed);
        if (failure is not null)
        {
            Diagnostics.Act("exit code", $"{(int)failure.ExitCode} ({failure.ExitCode})");
            Diagnostics.Act("error message", failure.ErrorMessage);
        }
    }

    private void ActAndAssertThrown(ArgumentNullException exception)
    {
        Diagnostics.Act("exception", exception.GetType().Name + " for " + exception.ParamName);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private UploadTransferTarget[] ResolveTransferTargets(UploadFileGlob glob, string url)
    {
        Diagnostics.Arrange("url", "\"" + url + "\"");
        UploadTransferTarget[] targets = [.. glob.ResolveTransferTargets(url)];
        Diagnostics.Act("targets", Describe(targets));
        return targets;
    }

    private UploadFileGlob Parse(string uploadFile, bool globOff)
    {
        ArrangeUploadFile(uploadFile, globOff);
        Assert.IsTrue(UploadFileGlob.TryParse(uploadFile, globOff, out UploadFileGlob? glob, out _));
        return glob;
    }
}
