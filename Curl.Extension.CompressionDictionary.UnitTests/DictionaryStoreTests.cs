using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.CompressionDictionary.UnitTests;

[TestClass]
public sealed class DictionaryStoreTests
{
    private static readonly Uri DictionaryUrl = new("https://example.com/app/dictionary.dat");

    private readonly SettableTimeProvider _time = new();
    private string _directory = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateDirectoryName() => _directory = Path.Combine(Path.GetTempPath(), "curl-dictionaries-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task StoreAsync_Dictionary_RoundTripsThroughTheDirectory()
    {
        var store = new DictionaryStore(_directory, _time);
        var content = Encoding.ASCII.GetBytes("dictionary bytes");

        var stored = await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/app/*", ["document", "frame"], "v1"), content, TestContext.CancellationToken);
        var found = await store.FindBestMatchAsync(new Uri("https://example.com/app/main.js"), TestContext.CancellationToken);

        CollectionAssert.AreEqual(SHA256.HashData(content), stored.Hash);
        Assert.IsNotNull(found);
        CollectionAssert.AreEqual(stored.Hash, found.Hash);
        Assert.AreEqual(DictionaryUrl, found.Url);
        Assert.AreEqual("v1", found.Directives.Id);
        CollectionAssert.AreEqual(new[] { "document", "frame" }, found.Directives.MatchDestinations.ToArray());
        Assert.AreEqual(_time.Now.ToUnixTimeMilliseconds(), found.FetchedAt.ToUnixTimeMilliseconds());
        CollectionAssert.AreEqual(content, await store.ReadContentAsync(found, TestContext.CancellationToken));
    }

    // RFC 9842 section 2.2.2: the longest match wins, then the most recently fetched.
    [TestMethod]
    public async Task FindBestMatchAsync_SeveralMatch_PicksLongestThenNewest()
    {
        var store = new DictionaryStore(_directory, _time);
        await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/app/*", [], "short"), new byte[] { 1 }, TestContext.CancellationToken);
        _time.Now = _time.Now.AddMinutes(1);
        await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/app/js/*", [], "older"), new byte[] { 2 }, TestContext.CancellationToken);
        _time.Now = _time.Now.AddMinutes(1);
        await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/app/js/*", [], "newer"), new byte[] { 3 }, TestContext.CancellationToken);
        _time.Now = _time.Now.AddMinutes(1);
        await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/app/*", [], "short-newest"), new byte[] { 4 }, TestContext.CancellationToken);
        await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/other/*/longer", [], "no-match"), new byte[] { 5 }, TestContext.CancellationToken);
        await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/app/(x)/*/longer", [], "ignored"), new byte[] { 6 }, TestContext.CancellationToken);

        var jsMatch = await store.FindBestMatchAsync(new Uri("https://example.com/app/js/main.js"), TestContext.CancellationToken);
        var cssMatch = await store.FindBestMatchAsync(new Uri("https://example.com/app/css/main.css"), TestContext.CancellationToken);
        var noMatch = await store.FindBestMatchAsync(new Uri("https://example.com/none"), TestContext.CancellationToken);

        Assert.AreEqual("newer", jsMatch?.Directives.Id);
        Assert.AreEqual("short-newest", cssMatch?.Directives.Id);
        Assert.IsNull(noMatch);
    }

    [TestMethod]
    public async Task FindBestMatchAsync_NoDirectory_ReturnsNull() =>
        Assert.IsNull(await new DictionaryStore(_directory, _time).FindBestMatchAsync(DictionaryUrl, TestContext.CancellationToken));

    [TestMethod]
    [DataRow("")]
    [DataRow("zz\nhttps://example.com/\n/*\n\n0\n")]
    [DataRow("00\nhttps://example.com/\n/*\n\n0\n")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000\nnot a url\n/*\n\n0\n")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000\nhttps://example.com/\n/*\n\n-1\n")]
    public async Task FindBestMatchAsync_CorruptMetadata_IsSkipped(string metadata)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "corrupt.metadata"), metadata, TestContext.CancellationToken);

        Assert.IsNull(await new DictionaryStore(_directory, _time).FindBestMatchAsync(DictionaryUrl, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadContentAsync_FileGone_ReturnsNull()
    {
        var store = new DictionaryStore(_directory, _time);
        var stored = await store.StoreAsync(DictionaryUrl, new UseAsDictionary("/*", [], ""), new byte[] { 1 }, TestContext.CancellationToken);
        File.Delete(Path.Combine(_directory, Convert.ToHexStringLower(stored.Hash) + ".dictionary"));

        Assert.IsNull(await store.ReadContentAsync(stored, TestContext.CancellationToken));
    }

    // RFC 9842 section 2.2.2: longer match first, then most recently fetched.
    [TestMethod]
    [DataRow("/ab", 0, true)]
    [DataRow("/a", 1, true)]
    [DataRow("/a", 0, false)]
    [DataRow("/a", -1, false)]
    [DataRow("/", 1, false)]
    public void IsBetter_Candidate_ComparesLengthThenFetchTime(string match, int minutesLater, bool expected)
    {
        var best = new StoredDictionary(DictionaryUrl, new UseAsDictionary("/a", [], ""), [], _time.Now);
        var candidate = new StoredDictionary(DictionaryUrl, new UseAsDictionary(match, [], ""), [], _time.Now.AddMinutes(minutesLater));

        Assert.AreEqual(expected, DictionaryStore.IsBetter(candidate, best));
        Assert.IsTrue(DictionaryStore.IsBetter(candidate, null));
    }

    private sealed class SettableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
