using System.Text;
using Curl.Extension.WebDavTools.Sync;
using Curl.Extension.WebDavTools.Transfer;

namespace Curl.Extension.WebDavTools.UnitTests.Transfer;

[TestClass]
public sealed class WebDavSyncTransferHookTests
{
    private const string LastModified = "Wed, 28 Oct 2026 10:00:00 GMT";
    private static readonly DateTime LastModifiedUtc = new(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);
    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.com/dav/");

    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() => folder = Path.Combine(Path.GetTempPath(), "webdav-sync-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteFolder()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ACollectionTree_DownloadsEachFileAndSyncsEachSubCollection_Rfc4918Sections9_1And15_9()
    {
        var origin = Tree();
        var output = new MemoryStream();

        var result = await Hook().ExecuteAsync(Context(output), origin.Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(207, result.Report!.ResponseCode);
        CollectionAssert.AreEqual(
            new[] { "PROPFIND /dav/", "GET /dav/a%20b.txt", "PROPFIND /dav/sub/", "GET /dav/sub/c.txt" },
            origin.RequestLines.ToArray());
        Assert.AreEqual("A B", File.ReadAllText(Path.Combine(folder, "a b.txt")));
        Assert.AreEqual("C", File.ReadAllText(Path.Combine(folder, "sub", "c.txt")));
        Assert.AreEqual(
            $"downloaded\t{Path.Combine(folder, "a b.txt")}\ndownloaded\t{Path.Combine(folder, "sub", "c.txt")}\n",
            Encoding.UTF8.GetString(output.ToArray()));
        Assert.IsFalse(File.Exists(Path.Combine(folder, "a b.txt" + WebDavSyncTransferHook.PartialFileSuffix)));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFileWithGetLastModified_SetsItAsTheLastWriteTimeAndIsCurrentNextTime_Rfc4918Section15_7()
    {
        await Hook().ExecuteAsync(Context(new MemoryStream()), Tree().Invoke);
        Assert.AreEqual(LastModifiedUtc, File.GetLastWriteTimeUtc(Path.Combine(folder, "a b.txt")));
        var origin = Tree();
        var output = new MemoryStream();

        await Hook().ExecuteAsync(Context(output), origin.Invoke);

        Assert.DoesNotContain("GET /dav/a%20b.txt", origin.RequestLines.ToList());
        StringAssert.StartsWith(Encoding.UTF8.GetString(output.ToArray()), $"current\t{Path.Combine(folder, "a b.txt")}\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_AnHrefOutsideTheCollection_IsRefusedAndNothingIsWrittenForIt()
    {
        var origin = new DavOrigin();
        origin.Responses["PROPFIND /dav/"] = (207, DavOrigin.Multistatus(DavOrigin.File("/other/x.txt", 1, null)));
        var output = new MemoryStream();

        var result = await Hook().ExecuteAsync(Context(output), origin.Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("refused\t/other/x.txt\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.IsEmpty(Directory.GetFileSystemEntries(folder));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFileTheServerDoesNotServe_FailsWithExit22AndLeavesNoFile()
    {
        var origin = new DavOrigin();
        origin.Responses["PROPFIND /dav/"] = (207, DavOrigin.Multistatus(DavOrigin.File("/dav/gone.txt", 1, null)));

        var result = await Hook().ExecuteAsync(Context(new MemoryStream()), origin.Invoke);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The server answered GET /dav/gone.txt with 404", result.ErrorMessage);
        Assert.IsEmpty(Directory.GetFileSystemEntries(folder));
    }

    [TestMethod]
    public async Task ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22()
    {
        var listing = DavOrigin.Multistatus(DavOrigin.File("/dav/a.txt", 1, null));
        ValueTask<TransferResult> Answer(ITransferContext context) => context.Http!.CustomMethod == "GET"
            ? ValueTask.FromResult(TransferResult.Success(0))
            : AnswerListing(context, listing);

        var result = await Hook().ExecuteAsync(Context(new MemoryStream()), Answer);

        Assert.AreEqual("The server answered GET /dav/a.txt with 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedDownload_GivesItsResultAndLeavesNoFile()
    {
        var origin = Tree();
        var failure = TransferResult.Failure(CurlExitCode.PartialFile, "cut short");
        origin.Failures["GET /dav/a%20b.txt"] = failure;

        Assert.AreSame(failure, await Hook().ExecuteAsync(Context(new MemoryStream()), origin.Invoke));
        Assert.IsEmpty(Directory.GetFileSystemEntries(folder));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedSubCollectionListing_GivesItsResult()
    {
        var origin = Tree();
        origin.Responses.Remove("PROPFIND /dav/sub/");

        var result = await Hook().ExecuteAsync(Context(new MemoryStream()), origin.Invoke);

        Assert.AreEqual("The server answered PROPFIND with 404, not 207 Multistatus", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFolderPathThatIsAFile_FailsWithExit23()
    {
        File.WriteAllText(folder, "in the way");
        try
        {
            var result = await Hook().ExecuteAsync(Context(new MemoryStream()), Tree().Invoke);

            Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
            StringAssert.StartsWith(result.ErrorMessage, "--webdav-sync could not write to the local folder: ");
        }
        finally
        {
            File.Delete(folder);
        }
    }

    [TestMethod]
    [DataRow("http://example.com/dav", "http://example.com/dav/a%20b.txt")]
    [DataRow("http://example.com/dav/?x=1", "http://example.com/dav/a%20b.txt")]
    [DataRow("http://example.com/dav/#top", "http://example.com/dav/a%20b.txt")]
    public void MemberUrl_AFile_IsTheCollectionPathAndTheEncodedNameWithoutQuery(string collection, string expected)
    {
        var url = WebDavSyncTransferHook.MemberUrl(CurlUrl.Parse(collection), new WebDavSyncItem("/dav/a b.txt", Path.Combine("x", "a b.txt"), null), false);

        Assert.AreEqual(expected, url.OriginalString);
    }

    [TestMethod]
    public void MemberUrl_ACollection_EndsInASlash()
    {
        var url = WebDavSyncTransferHook.MemberUrl(Url, new WebDavSyncItem("/dav/sub/", Path.Combine("x", "sub"), null), true);

        Assert.AreEqual("http://example.com/dav/sub/", url.OriginalString);
    }

    private WebDavSyncTransferHook Hook() => new(folder, new WebDavDiskFileSystem());

    private static DavOrigin Tree()
    {
        var origin = new DavOrigin();
        origin.Responses["PROPFIND /dav/"] = (207, DavOrigin.Multistatus(
            DavOrigin.Collection("/dav/"),
            DavOrigin.File("/dav/a%20b.txt", 3, LastModified),
            DavOrigin.Collection("/dav/sub/")));
        origin.Responses["PROPFIND /dav/sub/"] = (207, DavOrigin.Multistatus(DavOrigin.Collection("/dav/sub/"), DavOrigin.File("/dav/sub/c.txt", 1, null)));
        origin.Responses["GET /dav/a%20b.txt"] = (200, "A B");
        origin.Responses["GET /dav/sub/c.txt"] = (200, "C");
        return origin;
    }

    private static async ValueTask<TransferResult> AnswerListing(ITransferContext context, string listing)
    {
        await context.Output.WriteAsync(Encoding.UTF8.GetBytes(listing));
        return TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 207 } };
    }

    private static TransferContext Context(Stream output) => new() { Url = Url, Output = output, Http = new HttpRequestOptions() };
}
