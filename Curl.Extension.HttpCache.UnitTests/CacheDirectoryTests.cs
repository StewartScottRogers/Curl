namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class CacheDirectoryTests
{
    private static readonly DateTimeOffset RequestTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateDirectoryName() => _directory = Path.Combine(Path.GetTempPath(), "curl-httpcache-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static StoredResponse Sample() =>
        new("HTTP/1.1 200 OK", 200, [new("Content-Type", "text/plain"), new("ETag", "\"é1\"")], "hello"u8.ToArray(), RequestTime, RequestTime.AddSeconds(1));

    private static void AssertSame(StoredResponse expected, StoredResponse? actual)
    {
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.StatusLine, actual.StatusLine);
        Assert.AreEqual(expected.StatusCode, actual.StatusCode);
        CollectionAssert.AreEqual(expected.Headers.ToArray(), actual.Headers.ToArray());
        CollectionAssert.AreEqual(expected.Body, actual.Body);
        Assert.AreEqual(expected.RequestTime, actual.RequestTime);
        Assert.AreEqual(expected.ResponseTime, actual.ResponseTime);
    }

    [TestMethod]
    public async Task Section2_WrittenEntry_ReadsBackWhole()
    {
        var cache = new CacheDirectory(_directory);
        var key = CacheKey.For("GET", new Uri("http://example.com/a?b=1"));
        await cache.WriteAsync(key, Sample(), TestContext.CancellationToken);

        AssertSame(Sample(), await cache.ReadAsync(key, TestContext.CancellationToken));
        CollectionAssert.AreEqual(new[] { key + ".entry" }, Directory.GetFiles(_directory).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public async Task Section2_SecondWrite_ReplacesTheEntry()
    {
        var cache = new CacheDirectory(_directory);
        var key = CacheKey.For("GET", new Uri("http://example.com/"));
        await cache.WriteAsync(key, Sample() with { StatusLine = "HTTP/1.1 200 Old" }, TestContext.CancellationToken);
        await cache.WriteAsync(key, Sample(), TestContext.CancellationToken);

        AssertSame(Sample(), await cache.ReadAsync(key, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MissingEntry_ReadsAsNoEntry()
    {
        Directory.CreateDirectory(_directory);
        Assert.IsNull(await new CacheDirectory(_directory).ReadAsync(CacheKey.For("GET", new Uri("http://example.com/")), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MissingDirectory_ReadsAsNoEntry() =>
        Assert.IsNull(await new CacheDirectory(_directory).ReadAsync(CacheKey.For("GET", new Uri("http://example.com/")), TestContext.CancellationToken));

    [TestMethod]
    public async Task TruncatedEntryFile_ReadsAsNoEntry()
    {
        var cache = new CacheDirectory(_directory);
        var key = CacheKey.For("GET", new Uri("http://example.com/"));
        await cache.WriteAsync(key, Sample(), TestContext.CancellationToken);
        var path = Path.Combine(_directory, key + ".entry");
        var bytes = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        await File.WriteAllBytesAsync(path, bytes[..^1], TestContext.CancellationToken);

        Assert.IsNull(await cache.ReadAsync(key, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Section2_Key_DependsOnMethodAndTargetUriButNotFragmentOrUserInformation()
    {
        var key = CacheKey.For("GET", new Uri("http://example.com/a"));
        Assert.AreEqual(64, key.Length);
        Assert.AreEqual(key, CacheKey.For("GET", new Uri("http://user:pw@example.com/a#top")));
        Assert.AreNotEqual(key, CacheKey.For("HEAD", new Uri("http://example.com/a")));
        Assert.AreNotEqual(key, CacheKey.For("GET", new Uri("http://example.com/a?x")));
        Assert.AreNotEqual(key, CacheKey.For("GET", new Uri("https://example.com/a")));
    }

    [TestMethod]
    public void EncodedEntry_DecodesWhole() => AssertSame(Sample(), StoredResponseEncoding.Decode(StoredResponseEncoding.Encode(Sample())));

    [TestMethod]
    public void EveryTruncation_DecodesAsNoEntry()
    {
        var bytes = StoredResponseEncoding.Encode(Sample());
        for (var length = 0; length < bytes.Length; length++)
        {
            Assert.IsNull(StoredResponseEncoding.Decode(bytes[..length]), $"length {length}");
        }
    }

    [TestMethod]
    public void TrailingBytes_DecodeAsNoEntry() =>
        Assert.IsNull(StoredResponseEncoding.Decode([.. StoredResponseEncoding.Encode(Sample()), 0]));

    [TestMethod]
    public void WrongMagic_DecodesAsNoEntry()
    {
        var bytes = StoredResponseEncoding.Encode(Sample());
        bytes[0] ^= 0xFF;
        Assert.IsNull(StoredResponseEncoding.Decode(bytes));
    }

    [TestMethod]
    public void BadStringLength_DecodesAsNoEntry() =>
        Assert.IsNull(StoredResponseEncoding.Decode(Corrupt(offsetAfterMagic: 4, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF])));

    [TestMethod]
    public void TicksOutOfRange_DecodeAsNoEntry() =>
        Assert.IsNull(StoredResponseEncoding.Decode(Corrupt(offsetAfterMagic: 4 + 1 + "HTTP/1.1 200 OK".Length, BitConverter.GetBytes(long.MaxValue))));

    [TestMethod]
    public void NegativeBodyLength_DecodesAsNoEntry()
    {
        var bytes = StoredResponseEncoding.Encode(Sample());
        BitConverter.GetBytes(-1).CopyTo(bytes, bytes.Length - 5 - 4);
        Assert.IsNull(StoredResponseEncoding.Decode(bytes));
    }

    private static byte[] Corrupt(int offsetAfterMagic, byte[] replacement)
    {
        var bytes = StoredResponseEncoding.Encode(Sample());
        replacement.CopyTo(bytes, "CURL-HTTP-CACHE 1\n".Length + offsetAfterMagic);
        return bytes;
    }
}
