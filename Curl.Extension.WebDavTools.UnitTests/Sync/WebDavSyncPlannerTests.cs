using Curl.Extension.WebDavTools.Multistatus;
using Curl.Extension.WebDavTools.Sync;

namespace Curl.Extension.WebDavTools.UnitTests.Sync;

[TestClass]
public sealed class WebDavSyncPlannerTests
{
    private const string LocalFolder = "local";

    private static readonly DateTimeOffset Modified = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryFileSystem fileSystem = new();

    // RFC 4918 section 15.9: resourcetype holding collection marks a sub-collection, listed next, never downloaded.
    [TestMethod]
    public void Plan_CollectionEntry_IsListedNextAsASubCollection()
    {
        var plan = Plan("/dav/", Directory("/dav/sub/"));

        Assert.AreEqual(new WebDavSyncItem("/dav/sub/", Path.Combine(LocalFolder, "sub"), Modified), plan.SubCollections.Single());
        Assert.IsEmpty(plan.Downloads);
    }

    // RFC 4918 section 9.1: a Depth 1 listing also holds the requested collection; the sync skips it.
    [TestMethod]
    [DataRow("/dav/")]
    [DataRow("/dav")]
    [DataRow("http://example.com/dav/")]
    public void Plan_RequestedCollectionItself_IsSkipped(string href)
    {
        var plan = Plan("/dav", Directory(href));

        Assert.IsEmpty(plan.SubCollections);
        Assert.IsEmpty(plan.Refusals);
    }

    [TestMethod]
    public void Plan_FileMissingLocally_IsDownloadedWithItsLastModified()
    {
        var plan = Plan("/dav/", File("/dav/a.txt", 5, Modified));

        Assert.AreEqual(new WebDavSyncItem("/dav/a.txt", Path.Combine(LocalFolder, "a.txt"), Modified), plan.Downloads.Single());
    }

    // RFC 4918 sections 15.4 and 15.7: the same length and a last-write time no older than getlastmodified is current.
    [TestMethod]
    [DataRow(0)]
    [DataRow(60)]
    public void Plan_LocalFileWithSameLengthAndNoOlder_IsCurrent(int secondsNewer)
    {
        fileSystem.Add(Path.Combine(LocalFolder, "a.txt"), 5, Modified.AddSeconds(secondsNewer));

        var plan = Plan("/dav/", File("/dav/a.txt", 5, Modified));

        Assert.AreEqual("/dav/a.txt", plan.CurrentFiles.Single().Href);
        Assert.IsEmpty(plan.Downloads);
    }

    [TestMethod]
    public void Plan_LocalFileOlderThanGetlastmodified_IsDownloaded()
    {
        fileSystem.Add(Path.Combine(LocalFolder, "a.txt"), 5, Modified.AddSeconds(-1));

        var plan = Plan("/dav/", File("/dav/a.txt", 5, Modified));

        Assert.AreEqual("/dav/a.txt", plan.Downloads.Single().Href);
    }

    // RFC 4918 section 15.4: a different getcontentlength means the local copy is not current.
    [TestMethod]
    public void Plan_LocalFileOfOtherLength_IsDownloaded()
    {
        fileSystem.Add(Path.Combine(LocalFolder, "a.txt"), 4, Modified);

        var plan = Plan("/dav/", File("/dav/a.txt", 5, Modified));

        Assert.AreEqual("/dav/a.txt", plan.Downloads.Single().Href);
    }

    [TestMethod]
    public void Plan_ListingWithoutGetlastmodified_IsDownloaded()
    {
        fileSystem.Add(Path.Combine(LocalFolder, "a.txt"), 5, Modified);

        var plan = Plan("/dav/", File("/dav/a.txt", 5, null));

        Assert.AreEqual(new WebDavSyncItem("/dav/a.txt", Path.Combine(LocalFolder, "a.txt"), null), plan.Downloads.Single());
    }

    [TestMethod]
    public void Plan_ListingWithoutGetcontentlength_IsDownloaded()
    {
        fileSystem.Add(Path.Combine(LocalFolder, "a.txt"), 5, Modified);

        var plan = Plan("/dav/", File("/dav/a.txt", null, Modified));

        Assert.AreEqual("/dav/a.txt", plan.Downloads.Single().Href);
    }

    // RFC 4918 section 8.3: an href may be an absolute URL; only its path is mapped.
    [TestMethod]
    public void Plan_AbsoluteUrlHref_IsMappedByItsPath()
    {
        var plan = Plan("/dav/", File("https://example.com/dav/a.txt", 5, Modified));

        Assert.AreEqual(new WebDavSyncItem("https://example.com/dav/a.txt", Path.Combine(LocalFolder, "a.txt"), Modified), plan.Downloads.Single());
    }

    [TestMethod]
    public void Plan_MixedListing_SortsEachEntryInListingOrder()
    {
        fileSystem.Add(Path.Combine(LocalFolder, "same.txt"), 1, Modified);

        var plan = Plan("/dav/", Directory("/dav/"), File("/dav/new.txt", 1, Modified), Directory("/dav/x/"), File("/dav/same.txt", 1, Modified), File("/other/a", 1, Modified));

        CollectionAssert.AreEqual(new[] { "/dav/new.txt" }, plan.Downloads.Select(item => item.Href).ToArray());
        CollectionAssert.AreEqual(new[] { "/dav/x/" }, plan.SubCollections.Select(item => item.Href).ToArray());
        CollectionAssert.AreEqual(new[] { "/dav/same.txt" }, plan.CurrentFiles.Select(item => item.Href).ToArray());
        CollectionAssert.AreEqual(new[] { "/other/a" }, plan.Refusals.Select(refusal => refusal.Href).ToArray());
    }

    [TestMethod]
    [DataRow("/other/a.txt")]
    [DataRow("/davx/a.txt")]
    [DataRow("/")]
    [DataRow("http://example.com")]
    [DataRow("http://example.com/elsewhere/a.txt")]
    public void Plan_HrefOutsideTheCollection_IsRefused(string href)
    {
        var plan = Plan("/dav/", File(href, 1, Modified));

        Assert.AreEqual(new WebDavSyncRefusal(href, WebDavSyncRefusalKind.OutsideCollection), plan.Refusals.Single());
        Assert.IsEmpty(plan.Downloads);
    }

    [TestMethod]
    [DataRow("/dav/..")]
    [DataRow("/dav/../")]
    [DataRow("/dav/.")]
    [DataRow("/dav/../etc/passwd")]
    [DataRow("/dav/a/b.txt")]
    [DataRow("/dav/a\\..\\..\\b.txt")]
    [DataRow("/dav/a//")]
    public void Plan_HrefWithATraversalSegment_IsRefused(string href)
    {
        var plan = Plan("/dav/", File(href, 1, Modified), Directory(href));

        Assert.HasCount(2, plan.Refusals);
        Assert.IsTrue(plan.Refusals.All(refusal => refusal == new WebDavSyncRefusal(href, WebDavSyncRefusalKind.TraversalSegment)));
        Assert.IsEmpty(plan.Downloads);
        Assert.IsEmpty(plan.SubCollections);
    }

    [TestMethod]
    [DataRow("/dav//")]
    [DataRow("/dav/a:b")]
    [DataRow("/dav/a*")]
    [DataRow("/dav/a?")]
    [DataRow("/dav/<a>")]
    [DataRow("/dav/a\"")]
    [DataRow("/dav/a|b")]
    [DataRow("/dav/a\u0001")]
    public void Plan_HrefNamingAnInvalidFileName_IsRefused(string href)
    {
        var plan = Plan("/dav/", File(href, 1, Modified));

        Assert.AreEqual(new WebDavSyncRefusal(href, WebDavSyncRefusalKind.InvalidFileName), plan.Refusals.Single());
    }

    [TestMethod]
    public void Plan_NullArgument_Throws()
    {
        var planner = new WebDavSyncPlanner(fileSystem);

        Assert.ThrowsExactly<ArgumentNullException>(() => planner.Plan(null!, [], LocalFolder));
        Assert.ThrowsExactly<ArgumentNullException>(() => planner.Plan("/dav/", null!, LocalFolder));
        Assert.ThrowsExactly<ArgumentNullException>(() => planner.Plan("/dav/", [], null!));
    }

    private static WebDavResourceEntry Directory(string href) => new(href, true, null, Modified, null, 200);

    private static WebDavResourceEntry File(string href, long? length, DateTimeOffset? lastModified) =>
        new(href, false, length, lastModified, "\"e\"", 200);

    private WebDavSyncPlan Plan(string collectionPath, params WebDavResourceEntry[] entries) =>
        new WebDavSyncPlanner(fileSystem).Plan(collectionPath, entries, LocalFolder);

    private sealed class InMemoryFileSystem : IWebDavLocalFileSystem
    {
        private readonly Dictionary<string, WebDavLocalFile> files = [];

        public void Add(string path, long length, DateTimeOffset lastWriteTime) => files[path] = new WebDavLocalFile(length, lastWriteTime);

        public WebDavLocalFile? FindFile(string path) => files.GetValueOrDefault(path);
    }
}
