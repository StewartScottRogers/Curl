namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// Pins the cache file (ADR-1034): what it stores reads back, it is replaced whole, a missing or corrupt file reads as
/// empty, a failed write leaves no temporary file, and it is created owner-only where the OS has Unix permissions.
/// </summary>
[TestClass]
public sealed class OAuth2TokenCacheFileTests
{
    private static readonly OAuth2TokenCacheKey Key = new(new Uri("https://server.example.com/token"), "s6BhdRkqt3", "read write");

    private static readonly OAuth2CachedToken Token = new(
        "2YotnFZFEjr1zCsicMWpAA", "Bearer", DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), "tGzv3JOkF0XG5Qx2TlKWIA", "read");

    [TestMethod]
    public async Task WriteAsync_ThenReadAsync_ReadsBackEveryEntry()
    {
        using var directory = new CacheTestDirectory();
        var file = new OAuth2TokenCacheFile(directory.Path);
        var otherKey = new OAuth2TokenCacheKey(new Uri("http://localhost:8080/oauth/token"), "public", null);
        var otherToken = new OAuth2CachedToken("abc", "Bearer", null, null, null);

        var written = await file.WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> { [Key] = Token, [otherKey] = otherToken }, CancellationToken.None);
        var read = await file.ReadAsync(CancellationToken.None);

        Assert.IsTrue(written);
        Assert.AreEqual(directory.Combine(OAuth2TokenCacheFile.FileName), file.FilePath);
        Assert.AreEqual(directory.Path, file.Directory);
        Assert.HasCount(2, read);
        Assert.AreEqual(Token, read[Key]);
        Assert.AreEqual(otherToken, read[otherKey]);
        CollectionAssert.AreEqual(new[] { OAuth2TokenCacheFile.FileName }, System.IO.Directory.GetFiles(directory.Path).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_WithoutOwnerOnlyMode_ReplacesTheFileWholeInADirectoryItCreates()
    {
        using var directory = new CacheTestDirectory();
        var file = new OAuth2TokenCacheFile(directory.Combine("nested"), setsOwnerOnlyMode: false);

        await file.WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> { [Key] = Token }, CancellationToken.None);
        await file.WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken>(), CancellationToken.None);

        Assert.IsEmpty(await file.ReadAsync(CancellationToken.None));
        StringAssert.Contains(await File.ReadAllTextAsync(file.FilePath), "\"version\": 1");
    }

    [TestMethod]
    public async Task WriteAsync_Entry_WritesTheDocumentedFormat()
    {
        using var directory = new CacheTestDirectory();
        var file = new OAuth2TokenCacheFile(directory.Path, setsOwnerOnlyMode: false);

        await file.WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> { [Key] = Token with { RefreshToken = null } }, CancellationToken.None);

        var text = (await File.ReadAllTextAsync(file.FilePath)).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.AreEqual(
            "{\n  \"version\": 1,\n  \"entries\": [\n    {\n      \"token_endpoint\": \"https://server.example.com/token\",\n"
            + "      \"client_id\": \"s6BhdRkqt3\",\n      \"scope\": \"read write\",\n      \"access_token\": \"2YotnFZFEjr1zCsicMWpAA\",\n"
            + "      \"token_type\": \"Bearer\",\n      \"expires_at\": 1800000000,\n      \"refresh_token\": null,\n"
            + "      \"granted_scope\": \"read\"\n    }\n  ]\n}",
            text);
    }

    [TestMethod]
    public async Task ReadAsync_NoFile_ReadsAsEmpty()
    {
        using var directory = new CacheTestDirectory();

        Assert.IsEmpty(await new OAuth2TokenCacheFile(directory.Combine("missing")).ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{"version":2,"entries":[]}""")]
    [DataRow("""{"version":1.5,"entries":[]}""")]
    [DataRow("""{"version":1,"entries":{}}""")]
    [DataRow("""{"version":1,"entries":[{"token_endpoint":"https://a/t","client_id":"c","scope":null,"access_token":null,"token_type":"Bearer","expires_at":null,"refresh_token":null,"granted_scope":null}]}""")]
    [DataRow("""{"version":1,"entries":[{"token_endpoint":"relative/t","client_id":"c","scope":null,"access_token":"a","token_type":"Bearer","expires_at":null,"refresh_token":null,"granted_scope":null}]}""")]
    [DataRow("""{"version":1,"entries":[{"token_endpoint":"https://a/t","client_id":"c","scope":null,"access_token":"a","token_type":"Bearer","expires_at":999999999999999,"refresh_token":null,"granted_scope":null}]}""")]
    [DataRow("""{"version":1,"entries":[{"token_endpoint":"https://a/t","client_id":"c","scope":null,"access_token":"a","token_type":"Bearer","expires_at":"soon","refresh_token":null,"granted_scope":null}]}""")]
    [DataRow("""{"version":1,"entries":[{"token_endpoint":"https://a/t","client_id":"c","scope":null,"access_token":"a","token_type":"Bearer","expires_at":null,"refresh_token":null}]}""")]
    public async Task ReadAsync_CorruptFile_ReadsAsEmpty(string text)
    {
        using var directory = new CacheTestDirectory();
        await File.WriteAllTextAsync(directory.Combine(OAuth2TokenCacheFile.FileName), text);

        Assert.IsEmpty(await new OAuth2TokenCacheFile(directory.Path).ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteAsync_DirectoryIsAFile_ReturnsFalse()
    {
        using var directory = new CacheTestDirectory();
        var blocker = directory.Combine("blocker");
        await File.WriteAllTextAsync(blocker, "x");

        Assert.IsFalse(await new OAuth2TokenCacheFile(blocker).WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken>(), CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteAsync_FilePathIsADirectory_ReturnsFalseAndLeavesNoTemporaryFile()
    {
        using var directory = new CacheTestDirectory();
        System.IO.Directory.CreateDirectory(directory.Combine(OAuth2TokenCacheFile.FileName));

        var written = await new OAuth2TokenCacheFile(directory.Path).WriteAsync(
            new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> { [Key] = Token }, CancellationToken.None);

        Assert.IsFalse(written);
        Assert.IsEmpty(System.IO.Directory.GetFiles(directory.Path));
    }

    // FileStreamOptions.UnixCreateMode throws on Windows, so reaching it there proves the owner-only mode is handed to the OS.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task WriteAsync_OwnerOnlyModeOnWindows_ReachesUnixCreateMode()
    {
        using var directory = new CacheTestDirectory();
        var file = new OAuth2TokenCacheFile(directory.Path, setsOwnerOnlyMode: true);

        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
            () => file.WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken>(), CancellationToken.None));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task WriteAsync_OffWindows_CreatesTheFileOwnerOnly()
    {
        using var directory = new CacheTestDirectory();
        var file = new OAuth2TokenCacheFile(directory.Path);

        await file.WriteAsync(new Dictionary<OAuth2TokenCacheKey, OAuth2CachedToken> { [Key] = Token }, CancellationToken.None);

#pragma warning disable CA1416 // Excluded on Windows by the OSCondition above.
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file.FilePath));
#pragma warning restore CA1416
    }
}
