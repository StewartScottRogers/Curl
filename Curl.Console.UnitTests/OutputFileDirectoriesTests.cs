using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the directories <c>--create-dirs</c> creates for an output path, measured with curl
/// 8.21.0 on 2026-09-27 (BL-239 Notes).
/// </summary>
[TestClass]
public sealed class OutputFileDirectoriesTests
{
    private readonly InMemoryFileSystem paths = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CreateLeadingDirectories_NestedPath_CreatesEachDirectoryInOrder()
    {
        Diagnostics.Arrange("output path", "a/b/c.txt");
        Diagnostics.Arrange("runsOnWindows", false);

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "a/b/c.txt", runsOnWindows: false);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directories", string.Join(" | ", paths.CreatedDirectories));

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(failure);
        Diagnostics.Assert("created directories", "a | a/b", string.Join(" | ", paths.CreatedDirectories));
        CollectionAssert.AreEqual(new[] { "a", "a/b" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_OnWindowsBackslashes_SeparateDirectories()
    {
        Diagnostics.Arrange("output path", "x\\y\\z.txt");
        Diagnostics.Arrange("runsOnWindows", true);

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "x\\y\\z.txt", runsOnWindows: true);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directories", string.Join(" | ", paths.CreatedDirectories));

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(failure);
        Diagnostics.Assert("created directories", "x | x\\y", string.Join(" | ", paths.CreatedDirectories));
        CollectionAssert.AreEqual(new[] { "x", "x\\y" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_NotOnWindowsBackslashes_AreNameCharacters()
    {
        Diagnostics.Arrange("output path", "x\\y/z.txt");
        Diagnostics.Arrange("runsOnWindows", false);

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "x\\y/z.txt", runsOnWindows: false);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directories", string.Join(" | ", paths.CreatedDirectories));

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(failure);
        Diagnostics.Assert("created directories", "x\\y", string.Join(" | ", paths.CreatedDirectories));
        CollectionAssert.AreEqual(new[] { "x\\y" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_AbsoluteAndDoubledSeparators_SkipsTheEmptyParts()
    {
        Diagnostics.Arrange("output path", "/a//b/c.txt");
        Diagnostics.Arrange("runsOnWindows", false);

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "/a//b/c.txt", runsOnWindows: false);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directories", string.Join(" | ", paths.CreatedDirectories));

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(failure);
        Diagnostics.Assert("created directories", "/a | /a//b", string.Join(" | ", paths.CreatedDirectories));
        CollectionAssert.AreEqual(new[] { "/a", "/a//b" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_FileNameOnly_CreatesNothing()
    {
        Diagnostics.Arrange("output path", "c.txt");
        Diagnostics.Arrange("runsOnWindows", true);

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "c.txt", runsOnWindows: true);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directory count", paths.CreatedDirectories.Count);

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(failure);
        Diagnostics.Assert("created directory count", 0, paths.CreatedDirectories.Count);
        Assert.AreEqual(0, paths.CreatedDirectories.Count);
    }

    [TestMethod]
    public void CreateLeadingDirectories_DirectoryFails_GivesItAndStops()
    {
        paths.UncreatableDirectories.Add("blk/b");
        Diagnostics.Arrange("output path", "blk/b/c/d.txt");
        Diagnostics.Arrange("uncreatable directory", "blk/b");

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "blk/b/c/d.txt", runsOnWindows: false);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directories", string.Join(" | ", paths.CreatedDirectories));

        Diagnostics.Assert("failure", new DirectoryCreationFailure("blk/b", 22), failure);
        Assert.AreEqual(new DirectoryCreationFailure("blk/b", 22), failure);
        Diagnostics.Assert("created directories", "blk", string.Join(" | ", paths.CreatedDirectories));
        CollectionAssert.AreEqual(new[] { "blk" }, paths.CreatedDirectories);
    }

    [TestMethod]
    [DataRow(13)]
    [DataRow(17)]
    public void CreateLeadingDirectories_PermissionDeniedOrExists_SkipsToTheNextDirectory(int errorNumber)
    {
        paths.DirectoryErrorNumbers.Add("top", errorNumber);
        Diagnostics.Arrange("output path", "top/sub/f.txt");
        Diagnostics.Arrange("error number for top", errorNumber);

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "top/sub/f.txt", runsOnWindows: false);
        Diagnostics.Act("failure", failure);
        Diagnostics.Act("created directories", string.Join(" | ", paths.CreatedDirectories));

        Diagnostics.Assert("failure", null, failure);
        Assert.IsNull(failure);
        Diagnostics.Assert("created directories", "top/sub", string.Join(" | ", paths.CreatedDirectories));
        CollectionAssert.AreEqual(new[] { "top/sub" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_ReadOnlyFileSystem_GivesTheDirectoryWithItsErrno()
    {
        paths.DirectoryErrorNumbers.Add("top", 13);
        paths.DirectoryErrorNumbers.Add("top/sub", 30);
        Diagnostics.Arrange("output path", "top/sub/f.txt");
        Diagnostics.Arrange("error numbers", "top = 13, top/sub = 30");

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(paths, "top/sub/f.txt", runsOnWindows: false);
        Diagnostics.Act("failure", failure);

        Diagnostics.Assert("failure", new DirectoryCreationFailure("top/sub", 30), failure);
        Assert.AreEqual(new DirectoryCreationFailure("top/sub", 30), failure);
    }
}
