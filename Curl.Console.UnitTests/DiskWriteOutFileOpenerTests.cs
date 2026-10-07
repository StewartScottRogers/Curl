using System.Diagnostics.CodeAnalysis;
using Curl.Output;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Drives <see cref="DiskWriteOutFileOpener" /> against the real disk, in a fresh temporary
/// directory removed afterwards. None is <c>[TestCategory("Integration")]</c>: they need no
/// network, and the fast run must reach every line of <see cref="DiskWriteOutFileOpener" /> for its
/// coverage gate (BL-432), as <c>PhysicalOutputPathsTests</c> does for <c>PhysicalOutputPaths</c>.
/// </summary>
[TestClass]
public sealed class DiskWriteOutFileOpenerTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-write-out-files-" + Guid.NewGuid().ToString("N"));

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public async Task RenderAsync_TruncateThenAppendToTheSameFileInTextMode_MatchesCurl()
    {
        string file = Path.Combine(root, "o3.txt");
        File.WriteAllText(file, "old contents");
        WriteOutTemplateRenderer renderer = new(new DiskWriteOutFileOpener(writesLineFeedAsCrLf: true), false, WriteOutTimeDialect.WindowsCRuntime, TimeProvider.System);
        Diagnostics.Arrange("file held before", "old contents");
        Diagnostics.Arrange("write-out template", "%output{<temp>/o3.txt}F\\nG%output{>><temp>/o3.txt}H\\n");
        Diagnostics.Arrange("line feeds written as CRLF", true);

        await renderer.RenderAsync(
            $"%output{{{file}}}F\nG%output{{>>{file}}}H\n",
            new NoVariables(),
            new MemoryStream(),
            new MemoryStream());
        Diagnostics.Bytes("file bytes", File.ReadAllBytes(file));
        Diagnostics.Act("file length", File.ReadAllBytes(file).Length);

        Diagnostics.Diff("file bytes", "F\r\nGH\r\n"u8, File.ReadAllBytes(file));
        CollectionAssert.AreEqual("F\r\nGH\r\n"u8.ToArray(), File.ReadAllBytes(file));
    }

    [TestMethod]
    public void TryOpen_WithoutTextMode_WritesLineFeedsAsTheyAre()
    {
        string file = Path.Combine(root, "raw.txt");
        DiskWriteOutFileOpener opener = new(writesLineFeedAsCrLf: false);
        Diagnostics.Arrange("file", "<temp>/raw.txt");
        Diagnostics.Arrange("line feeds written as CRLF", false);

        Assert.IsTrue(opener.TryOpen(file, append: false, out Stream? stream));
        Diagnostics.Act("opened", true);
        Diagnostics.Assert("opened", true, true);
        using (stream)
        {
            stream.Write("a\n"u8);
        }

        Diagnostics.Diff("file bytes", "a\n"u8, File.ReadAllBytes(file));
        CollectionAssert.AreEqual("a\n"u8.ToArray(), File.ReadAllBytes(file));
    }

    [TestMethod]
    public void TryOpen_Append_KeepsWhatTheFileHeld()
    {
        string file = Path.Combine(root, "kept.txt");
        File.WriteAllText(file, "x");
        DiskWriteOutFileOpener opener = new(writesLineFeedAsCrLf: false);
        Diagnostics.Arrange("file held before", "x");
        Diagnostics.Arrange("written", "y");

        Assert.IsTrue(opener.TryOpen(file, append: true, out Stream? stream));
        Diagnostics.Act("opened", true);
        using (stream)
        {
            stream.Write("y"u8);
        }

        Diagnostics.Assert("file text", "xy", File.ReadAllText(file));
        Assert.AreEqual("xy", File.ReadAllText(file));
    }

    [TestMethod]
    public void TryOpen_EmptyName_IsFalse() => AssertRefused(string.Empty, "an empty name");

    [TestMethod]
    public void TryOpen_MissingDirectory_IsFalse() => AssertRefused(Path.Combine(root, "missing", "o.txt"), "<temp>/missing/o.txt");

    [TestMethod]
    public void TryOpen_Directory_IsFalse() => AssertRefused(root, "<temp>, a directory");

    [TestMethod]
    public void IsOpenFailure_TheExceptionsOpeningAFileRaises_AreTrue()
    {
        Diagnostics.Arrange("exceptions", "DirectoryNotFoundException, UnauthorizedAccessException, ArgumentException");

        bool[] failures =
        [
            DiskWriteOutFileOpener.IsOpenFailure(new DirectoryNotFoundException()),
            DiskWriteOutFileOpener.IsOpenFailure(new UnauthorizedAccessException()),
            DiskWriteOutFileOpener.IsOpenFailure(new ArgumentException()),
        ];
        Diagnostics.Act("open failures", string.Join(", ", failures));

        Diagnostics.Assert("open failures", "True, True, True", string.Join(", ", failures));
        Assert.IsTrue(DiskWriteOutFileOpener.IsOpenFailure(new DirectoryNotFoundException()));
        Assert.IsTrue(DiskWriteOutFileOpener.IsOpenFailure(new UnauthorizedAccessException()));
        Assert.IsTrue(DiskWriteOutFileOpener.IsOpenFailure(new ArgumentException()));
    }

    [TestMethod]
    public void IsOpenFailure_AnyOtherException_IsFalse()
    {
        Diagnostics.Arrange("exception", nameof(InvalidOperationException));

        bool failure = DiskWriteOutFileOpener.IsOpenFailure(new InvalidOperationException());
        Diagnostics.Act("open failure", failure);

        Diagnostics.Assert("open failure", false, failure);
        Assert.IsFalse(DiskWriteOutFileOpener.IsOpenFailure(new InvalidOperationException()));
    }

    private void AssertRefused(string path, string description)
    {
        Diagnostics.Arrange("path", description);
        foreach (bool append in new[] { false, true })
        {
            bool opened = new DiskWriteOutFileOpener(writesLineFeedAsCrLf: true).TryOpen(path, append, out Stream? stream);
            Diagnostics.Act($"opened (append {append})", opened);
            Diagnostics.Assert($"opened (append {append})", false, opened);
            Diagnostics.Assert($"stream (append {append})", "null", stream?.ToString() ?? "null");
            Assert.IsFalse(opened);
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
