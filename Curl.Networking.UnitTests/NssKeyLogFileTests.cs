using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="NssKeyLogFile" />: one NSS key-log line per secret, lowercase hex, appended; a
/// file that cannot be written is skipped silently, as curl's OpenSSL build skips one it cannot open.
/// </summary>
[TestClass]
public sealed class NssKeyLogFileTests
{
    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Append_TwoSecrets_AppendsOneLowercaseHexLineEachAfterWhatWasThere()
    {
        var path = Path.Combine(_directory, "keys.log");
        File.WriteAllText(path, "# earlier\n");
        var keyLog = new NssKeyLogFile(path);

        keyLog.Append("CLIENT_RANDOM", [0xAB, 0x01], [0xCD]);
        keyLog.Append("CLIENT_RANDOM", [0x02], [0xEF, 0x10]);
        var text = File.ReadAllText(path);
        Diagnostics.Act("file", text);

        Assert.AreEqual(path, keyLog.Path);
        Assert.AreEqual("# earlier\nCLIENT_RANDOM ab01 cd\nCLIENT_RANDOM 02 ef10\n", text);
    }

    [TestMethod]
    public void Append_FileInAMissingDirectory_WritesNothingAndDoesNotThrow()
    {
        var path = Path.Combine(_directory, "missing", "keys.log");
        var keyLog = new NssKeyLogFile(path);

        keyLog.Append("CLIENT_RANDOM", [0x01], [0x02]);
        Diagnostics.Act("file exists", File.Exists(path));

        Assert.IsFalse(File.Exists(path));
    }
}
