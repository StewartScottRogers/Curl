using System.Diagnostics.CodeAnalysis;
using Curl.Output;

namespace Curl.Console;

/// <summary>
/// Drives <see cref="DiskWriteOutFileOpener" /> against the real disk, in a fresh temporary
/// directory removed afterwards. The tests that open real files are
/// <c>[TestCategory("Integration")]</c> (BL-280); the fast run covers only the exception filter.
/// </summary>
[TestClass]
public sealed class DiskWriteOutFileOpenerTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-write-out-files-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RenderAsync_TruncateThenAppendToTheSameFileInTextMode_MatchesCurl()
    {
        string file = Path.Combine(root, "o3.txt");
        File.WriteAllText(file, "old contents");
        WriteOutTemplateRenderer renderer = new(new DiskWriteOutFileOpener(writesLineFeedAsCrLf: true), false, TimeProvider.System);

        await renderer.RenderAsync(
            $"%output{{{file}}}F\nG%output{{>>{file}}}H\n",
            new NoVariables(),
            new MemoryStream(),
            new MemoryStream());

        CollectionAssert.AreEqual("F\r\nGH\r\n"u8.ToArray(), File.ReadAllBytes(file));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void TryOpen_WithoutTextMode_WritesLineFeedsAsTheyAre()
    {
        string file = Path.Combine(root, "raw.txt");
        DiskWriteOutFileOpener opener = new(writesLineFeedAsCrLf: false);

        Assert.IsTrue(opener.TryOpen(file, append: false, out Stream? stream));
        using (stream)
        {
            stream.Write("a\n"u8);
        }

        CollectionAssert.AreEqual("a\n"u8.ToArray(), File.ReadAllBytes(file));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void TryOpen_Append_KeepsWhatTheFileHeld()
    {
        string file = Path.Combine(root, "kept.txt");
        File.WriteAllText(file, "x");
        DiskWriteOutFileOpener opener = new(writesLineFeedAsCrLf: false);

        Assert.IsTrue(opener.TryOpen(file, append: true, out Stream? stream));
        using (stream)
        {
            stream.Write("y"u8);
        }

        Assert.AreEqual("xy", File.ReadAllText(file));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void TryOpen_EmptyName_IsFalse() => AssertRefused(string.Empty);

    [TestMethod]
    [TestCategory("Integration")]
    public void TryOpen_MissingDirectory_IsFalse() => AssertRefused(Path.Combine(root, "missing", "o.txt"));

    [TestMethod]
    [TestCategory("Integration")]
    public void TryOpen_Directory_IsFalse() => AssertRefused(root);

    [TestMethod]
    public void IsOpenFailure_TheExceptionsOpeningAFileRaises_AreTrue()
    {
        Assert.IsTrue(DiskWriteOutFileOpener.IsOpenFailure(new DirectoryNotFoundException()));
        Assert.IsTrue(DiskWriteOutFileOpener.IsOpenFailure(new UnauthorizedAccessException()));
        Assert.IsTrue(DiskWriteOutFileOpener.IsOpenFailure(new ArgumentException()));
    }

    [TestMethod]
    public void IsOpenFailure_AnyOtherException_IsFalse() =>
        Assert.IsFalse(DiskWriteOutFileOpener.IsOpenFailure(new InvalidOperationException()));

    private static void AssertRefused(string path)
    {
        foreach (bool append in new[] { false, true })
        {
            Assert.IsFalse(new DiskWriteOutFileOpener(writesLineFeedAsCrLf: true).TryOpen(path, append, out Stream? stream));
            Assert.IsNull(stream);
        }
    }

    private sealed class NoVariables : IWriteOutVariableSource
    {
        public bool TransferFailed => false;

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            text = null;
            return false;
        }

        public string? FindFirstHeaderValue(string name) => null;
    }
}
