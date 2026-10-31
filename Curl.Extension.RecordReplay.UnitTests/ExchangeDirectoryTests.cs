namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class ExchangeDirectoryTests
{
    private string _directory = null!;

    [TestInitialize]
    public void CreatePath() => _directory = Path.Combine(Path.GetTempPath(), "recordreplay-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteAsync_AnExchange_IsReadBackFromKeyDotExchange_Adr1129Section1()
    {
        var directory = new ExchangeDirectory(_directory);

        await directory.WriteAsync("abc", new RecordedExchange("GET", "http://example.com/", 200, [1], [2]), CancellationToken.None);
        var read = await directory.ReadAsync("abc", CancellationToken.None);

        Assert.AreEqual(_directory, directory.Path);
        Assert.IsTrue(File.Exists(Path.Combine(_directory, "abc.exchange")));
        Assert.AreEqual(200, read!.StatusCode);
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    [TestMethod]
    public async Task ReadAsync_NoDirectoryNoFileOrNotAnExchange_GivesNone_Adr1129Section1()
    {
        var directory = new ExchangeDirectory(_directory);

        Assert.IsNull(await directory.ReadAsync("abc", CancellationToken.None));
        Directory.CreateDirectory(_directory);
        Assert.IsNull(await directory.ReadAsync("abc", CancellationToken.None));
        await File.WriteAllTextAsync(Path.Combine(_directory, "abc.exchange"), "not an exchange");
        Assert.IsNull(await directory.ReadAsync("abc", CancellationToken.None));
    }
}
