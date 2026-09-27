using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="UploadFileGlob" /> against curl 8.21.0 (mingw, Schannel), measured on
/// 2026-09-27 with <c>curl -s -S -w '%{url_effective}\n' -T '&lt;glob&gt;' http://127.0.0.1:1/g/</c>
/// (BL-031 Notes).
/// </summary>
[TestClass]
public sealed class UploadFileGlobTests
{
    [TestMethod]
    public void ResolveTransferTargets_SetGlob_SendsEachFileToItsOwnUrlInCurlsOrder()
    {
        UploadFileGlob glob = Parse("{local.txt,sub/in.txt}", globOff: false);

        UploadTransferTarget[] targets = [.. glob.ResolveTransferTargets("http://h/g/")];

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

        string[] urls = [.. glob.ResolveTransferTargets("http://h/g/").Select(target => target.TransferUrl)];

        Assert.AreEqual(3, glob.UploadFileCount);
        CollectionAssert.AreEqual(new[] { "f1.txt", "f2.txt", "f3.txt" }, glob.ExpandUploadFiles().ToArray());
        CollectionAssert.AreEqual(new[] { "http://h/g/f1.txt", "http://h/g/f2.txt", "http://h/g/f3.txt" }, urls);
    }

    [TestMethod]
    public void ResolveTransferTargets_EachFile_ResolvesAsUploadUrlAppendsIt()
    {
        UploadFileGlob glob = Parse("{a b.txt,c}", globOff: false);

        string[] urls = [.. glob.ResolveTransferTargets("http://h/g/").Select(target => target.TransferUrl)];

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

        UploadTransferTarget[] targets = [.. glob.ResolveTransferTargets("http://h/g/")];

        Assert.AreEqual(1, glob.UploadFileCount);
        Assert.HasCount(1, targets);
        Assert.AreEqual("{local.txt,sub/in.txt}", targets[0].UploadFile);
        Assert.AreEqual("http://h/g/in.txt%7D", targets[0].TransferUrl);
    }

    [TestMethod]
    public void ResolveTransferTargets_StandardInputAmongTheMatches_LeavesItsUrlUnchanged()
    {
        UploadFileGlob glob = Parse("{-,local.txt}", globOff: false);

        string[] urls = [.. glob.ResolveTransferTargets("http://h/g/").Select(target => target.TransferUrl)];

        CollectionAssert.AreEqual(new[] { "http://h/g/", "http://h/g/local.txt" }, urls);
    }

    [TestMethod]
    public void ResolveTransferTargets_MalformedUrl_MarksTheTargetNotWellFormed()
    {
        UploadFileGlob glob = Parse("f[1-2]", globOff: false);

        UploadTransferTarget[] targets = [.. glob.ResolveTransferTargets("http://h/d ir/")];

        Assert.HasCount(2, targets);
        Assert.IsFalse(targets[0].IsUrlWellFormed);
        Assert.AreEqual(string.Empty, targets[0].TransferUrl);
    }

    [TestMethod]
    public void ResolveTransferTargets_NullUrl_Throws()
    {
        UploadFileGlob glob = Parse("local.txt", globOff: false);

        Assert.ThrowsExactly<ArgumentNullException>(() => glob.ResolveTransferTargets(null!).ToArray());
    }

    [TestMethod]
    public void TryParse_MalformedGlob_IsUrlMalformatWithCurlsMessageAboutTheUploadArgument()
    {
        Assert.IsFalse(UploadFileGlob.TryParse("f[3-1].txt", globOff: false, out UploadFileGlob? glob, out TransferResult? failure));

        Assert.IsNull(glob);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("bad range in position 7:\nf[3-1].txt\n      ^", failure.ErrorMessage);
    }

    [TestMethod]
    public void TryParse_MalformedGlobUnderGlobOff_IsOneLiteralName()
    {
        Assert.IsTrue(UploadFileGlob.TryParse("f[3-1].txt", globOff: true, out UploadFileGlob? glob, out TransferResult? failure));

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { "f[3-1].txt" }, glob.ExpandUploadFiles().ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryParse_NullUploadFile_Throws(bool globOff)
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UploadFileGlob.TryParse(null!, globOff, out _, out _));
    }

    private static UploadFileGlob Parse(string uploadFile, bool globOff)
    {
        Assert.IsTrue(UploadFileGlob.TryParse(uploadFile, globOff, out UploadFileGlob? glob, out _));
        return glob;
    }
}
